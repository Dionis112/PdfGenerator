using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace PdfGenerator;


/// <summary>
/// <para>Rewrites FO incrementally and prepares bounded immutable image snapshots without loading the complete document tree.</para>
/// <para>Последовательно переписывает FO и подготавливает ограниченные неизменяемые снимки изображений без дерева всего документа.</para>
/// </summary>
internal sealed class FoImagePreparation : IDisposable
{

    /// <summary>
    /// <para>Gets or defines the FoNamespace value used by FoImagePreparation.</para>
    /// <para>Возвращает или определяет значение FoNamespace, используемое FoImagePreparation.</para>
    /// </summary>
    private const string FoNamespace = "http://www.w3.org/1999/XSL/Format";

    /// <summary>
    /// <para>Gets or defines the SvgNamespace value used by FoImagePreparation.</para>
    /// <para>Возвращает или определяет значение SvgNamespace, используемое FoImagePreparation.</para>
    /// </summary>
    private const string SvgNamespace = "http://www.w3.org/2000/svg";

    /// <summary>
    /// <para>Gets or defines the XmlNamespace value used by FoImagePreparation.</para>
    /// <para>Возвращает или определяет значение XmlNamespace, используемое FoImagePreparation.</para>
    /// </summary>
    private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

    /// <summary>
    /// <para>Gets or defines the directory value used by FoImagePreparation.</para>
    /// <para>Возвращает или определяет значение directory, используемое FoImagePreparation.</para>
    /// </summary>
    private readonly string directory;

    /// <summary>
    /// <para>Gets or defines the FoPath value used by FoImagePreparation.</para>
    /// <para>Возвращает или определяет значение FoPath, используемое FoImagePreparation.</para>
    /// </summary>
    public string FoPath
    {
        get;
    }

    /// <summary>
    /// <para>Cumulative preparation bytes charged to the temporary-file quota.</para>
    /// <para>Суммарные байты подготовки, учтённые в квоте временных файлов.</para>
    /// </summary>
    public long TemporaryBytes
    {
        get; private set;
    }

    /// <summary>
    /// <para>Initializes FoImagePreparation with an immutable snapshot of service settings.</para>
    /// <para>Создаёт FoImagePreparation со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="directory"><para>The directory argument.</para><para>Аргумент directory.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    private FoImagePreparation(string directory)
    {
        this.directory = directory;
        FoPath = Path.Combine(directory, "document.fo");
    }

    /// <summary>
    /// <para>Provides the Budget contract or implementation for PDF conversion.</para>
    /// <para>Предоставляет контракт или реализацию Budget для преобразования PDF.</para>
    /// </summary>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    private sealed class Budget(FopOptions options)
    {

        /// <summary>
        /// <para>Gets or defines the Images value used by Budget.</para>
        /// <para>Возвращает или определяет значение Images, используемое Budget.</para>
        /// </summary>
        public readonly Dictionary<string, string> Images = new(StringComparer.Ordinal);

        /// <summary>
        /// <para>Gets or defines the Count value used by Budget.</para>
        /// <para>Возвращает или определяет значение Count, используемое Budget.</para>
        /// </summary>
        public int Count;

        /// <summary>
        /// <para>Gets or defines the ImageBytes value used by Budget.</para>
        /// <para>Возвращает или определяет значение ImageBytes, используемое Budget.</para>
        /// </summary>
        public long ImageBytes;

        /// <summary>
        /// <para>Gets or defines the TemporaryBytes value used by Budget.</para>
        /// <para>Возвращает или определяет значение TemporaryBytes, используемое Budget.</para>
        /// </summary>
        public long TemporaryBytes;

        /// <summary>
        /// <para>Executes AddImage as part of the Budget conversion workflow.</para>
        /// <para>Выполняет AddImage в цепочке преобразования Budget.</para>
        /// </summary>
        /// <param name="bytes"><para>The bytes argument.</para><para>Аргумент bytes.</para></param>
        public void AddImage(long bytes)
        {
            if (++Count > options.MaxImages || bytes > options.MaxImageBytes || bytes > options.MaxTotalImageBytes - ImageBytes)
            {
                throw new IOException("Image count or aggregate byte limit exceeded.");
            }
            ImageBytes += bytes;
        }

        /// <summary>
        /// <para>Executes AddTemporary as part of the Budget conversion workflow.</para>
        /// <para>Выполняет AddTemporary в цепочке преобразования Budget.</para>
        /// </summary>
        /// <param name="bytes"><para>The bytes argument.</para><para>Аргумент bytes.</para></param>
        public void AddTemporary(long bytes)
        {
            if (bytes > options.MaxTemporaryBytes - TemporaryBytes)
            {
                throw new IOException("Temporary resource quota exceeded.");
            }
            TemporaryBytes += bytes;
        }
    }

    /// <summary>
    /// <para>Provides the BudgetStream contract or implementation for PDF conversion.</para>
    /// <para>Предоставляет контракт или реализацию BudgetStream для преобразования PDF.</para>
    /// </summary>
    /// <param name="inner"><para>The inner argument.</para><para>Аргумент inner.</para></param>
    /// <param name="budget"><para>The budget argument.</para><para>Аргумент budget.</para></param>
    private sealed class BudgetStream(Stream inner, Budget budget) : Stream
    {

        /// <summary>
        /// <para>Executes Write as part of the BudgetStream conversion workflow.</para>
        /// <para>Выполняет Write в цепочке преобразования BudgetStream.</para>
        /// </summary>
        /// <param name="buffer"><para>The buffer argument.</para><para>Аргумент buffer.</para></param>
        /// <param name="offset"><para>The offset argument.</para><para>Аргумент offset.</para></param>
        /// <param name="count"><para>The count argument.</para><para>Аргумент count.</para></param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            budget.AddTemporary(count);
            inner.Write(buffer, offset, count);
        }

        /// <summary>
        /// <para>Executes Write as part of the BudgetStream conversion workflow.</para>
        /// <para>Выполняет Write в цепочке преобразования BudgetStream.</para>
        /// </summary>
        /// <param name="buffer"><para>The buffer argument.</para><para>Аргумент buffer.</para></param>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            budget.AddTemporary(buffer.Length);
            inner.Write(buffer);
        }

        /// <summary>
        /// <para>Executes Flush as part of the BudgetStream conversion workflow.</para>
        /// <para>Выполняет Flush в цепочке преобразования BudgetStream.</para>
        /// </summary>
        public override void Flush() => inner.Flush();

        /// <summary>
        /// <para>Gets or defines the CanRead value used by BudgetStream.</para>
        /// <para>Возвращает или определяет значение CanRead, используемое BudgetStream.</para>
        /// </summary>
        public override bool CanRead => false;

        /// <summary>
        /// <para>Gets or defines the CanSeek value used by BudgetStream.</para>
        /// <para>Возвращает или определяет значение CanSeek, используемое BudgetStream.</para>
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// <para>Gets or defines the CanWrite value used by BudgetStream.</para>
        /// <para>Возвращает или определяет значение CanWrite, используемое BudgetStream.</para>
        /// </summary>
        public override bool CanWrite => true;

        /// <summary>
        /// <para>Gets or defines the Length value used by BudgetStream.</para>
        /// <para>Возвращает или определяет значение Length, используемое BudgetStream.</para>
        /// </summary>
        public override long Length => inner.Length;

        /// <summary>
        /// <para>Gets or defines the Position value used by BudgetStream.</para>
        /// <para>Возвращает или определяет значение Position, используемое BudgetStream.</para>
        /// </summary>
        public override long Position
        {
            get => inner.Position; set => throw new NotSupportedException();
        }

        /// <summary>
        /// <para>Executes Read as part of the BudgetStream conversion workflow.</para>
        /// <para>Выполняет Read в цепочке преобразования BudgetStream.</para>
        /// </summary>
        /// <param name="buffer"><para>The buffer argument.</para><para>Аргумент buffer.</para></param>
        /// <param name="offset"><para>The offset argument.</para><para>Аргумент offset.</para></param>
        /// <param name="count"><para>The count argument.</para><para>Аргумент count.</para></param>
        /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <summary>
        /// <para>Executes Seek as part of the BudgetStream conversion workflow.</para>
        /// <para>Выполняет Seek в цепочке преобразования BudgetStream.</para>
        /// </summary>
        /// <param name="offset"><para>The offset argument.</para><para>Аргумент offset.</para></param>
        /// <param name="origin"><para>The origin argument.</para><para>Аргумент origin.</para></param>
        /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <summary>
        /// <para>Executes SetLength as part of the BudgetStream conversion workflow.</para>
        /// <para>Выполняет SetLength в цепочке преобразования BudgetStream.</para>
        /// </summary>
        /// <param name="value"><para>The value argument.</para><para>Аргумент value.</para></param>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>
        /// <para>Releases owned resources; caller-owned input and output streams are not closed.</para>
        /// <para>Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются.</para>
        /// </summary>
        /// <param name="disposing"><para>The disposing argument.</para><para>Аргумент disposing.</para></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// <para>Executes CreateAsync as part of the FoImagePreparation conversion workflow.</para>
    /// <para>Выполняет CreateAsync в цепочке преобразования FoImagePreparation.</para>
    /// </summary>
    /// <param name="foPath"><para>The foPath argument.</para><para>Аргумент foPath.</para></param>
    /// <param name="targetDirectory"><para>The targetDirectory argument.</para><para>Аргумент targetDirectory.</para></param>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <param name="resourceBaseUri"><para>The resourceBaseUri argument.</para><para>Аргумент resourceBaseUri.</para></param>
    /// <param name="resources"><para>The resources argument.</para><para>Аргумент resources.</para></param>
    /// <param name="depth"><para>The depth argument.</para><para>Аргумент depth.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public static Task<FoImagePreparation> CreateAsync(string foPath, string targetDirectory, FopOptions options, CancellationToken token,
            Uri? resourceBaseUri = null, LocalResourcePolicy? resources = null, int depth = 0)
    {
        return Task.Run(() =>
        {
            var prepared = new FoImagePreparation(Path.Combine(targetDirectory, ".png-resources-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(prepared.directory);
            try
            {
                var budget = new Budget(options);
                budget.AddTemporary(new FileInfo(foPath).Length);
                prepared.Rewrite(foPath, prepared.FoPath, resourceBaseUri ?? new Uri(Path.GetFullPath(foPath)),
                    resources ?? new LocalResourcePolicy([Path.GetDirectoryName(Path.GetFullPath(foPath))!]), options, budget, depth, token);
                prepared.TemporaryBytes = budget.TemporaryBytes;
                return prepared;
            }
            catch { prepared.Dispose(); throw; }
        }, token);
    }

    /// <summary>
    /// <para>Executes Rewrite as part of the FoImagePreparation conversion workflow.</para>
    /// <para>Выполняет Rewrite в цепочке преобразования FoImagePreparation.</para>
    /// </summary>
    /// <param name="source"><para>The source argument.</para><para>Аргумент source.</para></param>
    /// <param name="target"><para>The target argument.</para><para>Аргумент target.</para></param>
    /// <param name="sourceUri"><para>The sourceUri argument.</para><para>Аргумент sourceUri.</para></param>
    /// <param name="resources"><para>The resources argument.</para><para>Аргумент resources.</para></param>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <param name="budget"><para>The budget argument.</para><para>Аргумент budget.</para></param>
    /// <param name="depth"><para>The depth argument.</para><para>Аргумент depth.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    private void Rewrite(string source, string target, Uri sourceUri, LocalResourcePolicy resources, FopOptions options, Budget budget, int depth, CancellationToken token)
    {
        if (depth > 16)
        {
            throw new IOException("SVG nesting limit exceeded.");
        }
        if (new FileInfo(source).Length > options.MaxFoBytes)
        {
            throw new IOException("FO exceeds its byte limit.");
        }
        using var reader = XmlReader.Create(source, new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = options.MaxFoBytes
        });
        using var writer = XmlWriter.Create(new BudgetStream(File.Create(target), budget), new()
        {
            Encoding = new UTF8Encoding(false),
            CloseOutput = true
        });
        var bases = new Stack<Uri>();
        var elements = new Stack<(string Namespace, string Name)>();
        char[] textBuffer = new char[65536];
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    string ns = reader.NamespaceURI;
                    string name = reader.LocalName;
                    bool svg = ns == SvgNamespace;
                    bool hasSvgFamily = svg && reader.GetAttribute("font-family") is not null;
                    if (svg && name is "script" or "foreignObject")
                    {
                        throw new InvalidDataException("Active SVG is disabled.");
                    }
                    Uri current = bases.Count == 0 ? sourceUri : bases.Peek();
                    string? xmlBase = reader.GetAttribute("base", XmlNamespace);
                    if (xmlBase is not null)
                    {
                        current = new Uri(current, xmlBase);
                    }
                    writer.WriteStartElement(reader.Prefix, name, ns);
                    bool root = bases.Count == 0;
                    bool empty = reader.IsEmptyElement;
                    if (reader.MoveToFirstAttribute())
                    {
                        do
                        {
                            string attr = reader.LocalName;
                            string attrNs = reader.NamespaceURI;
                            if (root && attr == "base" && attrNs == XmlNamespace)
                            {
                                continue;
                            }
                            writer.WriteStartAttribute(reader.Prefix, attr, attrNs);
                            bool image = ns == FoNamespace && (name == "external-graphic" && attr == "src" || attr == "background-image") || svg && name == "image" && attr == "href";
                            if (image)
                            {
                                string raw = Path.Combine(directory, "uri-" + Guid.NewGuid().ToString("N"));
                                try
                                {
                                    using (var rawWriter = new StreamWriter(new BudgetStream(File.Create(raw), budget), new UTF8Encoding(false)))
                                    {
                                        int count;
                                        long length = 0;
                                        while ((count = reader.ReadValueChunk(textBuffer, 0, textBuffer.Length)) != 0)
                                        {
                                            token.ThrowIfCancellationRequested();
                                            length += count;
                                            if (length > options.MaxImageBytes * 4L + 4096)
                                            {
                                                throw new IOException("Image URI exceeds its byte limit.");
                                            }
                                            rawWriter.Write(textBuffer, 0, count);
                                        }
                                    }
                                    writer.WriteString(PrepareReference(raw, current, resources, options, budget, depth, token));
                                }
                                finally { if (File.Exists(raw)) { File.Delete(raw); } }
                            }
                            else
                            {
                                string value = reader.Value;
                                if (svg)
                                {
                                    if (attr == "font-family")
                                    {
                                        value = ResolveSvgFont(value, options.SvgFontAliases);
                                    }
                                    if (attr.StartsWith("on", StringComparison.OrdinalIgnoreCase) || attr == "href" && !value.StartsWith('#'))
                                    {
                                        throw new InvalidDataException("Active/external SVG references are disabled.");
                                    }
                                    if (attr is "style" or "fill" or "stroke" or "filter" or "clip-path" or "mask" || value.Contains("url", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ValidateCss(value);
                                    }
                                }
                                writer.WriteString(value);
                            }
                            writer.WriteEndAttribute();
                        } while (reader.MoveToNextAttribute());
                        reader.MoveToElement();
                    }
                    if (root)
                    {
                        writer.WriteAttributeString("xml", "base", XmlNamespace, current.AbsoluteUri);
                    }
                    if (svg && name == "svg" && !hasSvgFamily)
                    {
                        writer.WriteAttributeString("font-family", "DejaVu Sans");
                    }
                    if (empty)
                    {
                        writer.WriteEndElement();
                    }
                    else
                    {
                        bases.Push(current);
                        elements.Push((ns, name));
                    }
                    break;
                case XmlNodeType.EndElement:
                    writer.WriteFullEndElement();
                    bases.Pop();
                    elements.Pop();
                    break;
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    if (elements.Count == 0 && reader.NodeType is XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
                    {
                        writer.WriteWhitespace(reader.Value);
                        break;
                    }
                    if (elements.TryPeek(out (string Namespace, string Name) element) && element == (SvgNamespace, "style"))
                    {
                        ValidateCss(reader.Value);
                        writer.WriteString(reader.Value);
                    }
                    else
                    {
                        int count;
                        while ((count = reader.ReadValueChunk(textBuffer, 0, textBuffer.Length)) != 0)
                        {
                            token.ThrowIfCancellationRequested();
                            writer.WriteChars(textBuffer, 0, count);
                        }
                    }
                    break;
                case XmlNodeType.Comment:
                    writer.WriteComment(reader.Value);
                    break;
                case XmlNodeType.ProcessingInstruction:
                    throw new InvalidDataException("Processing instructions are disabled.");
            }
        }
        writer.Flush();
    }

    /// <summary>
    /// <para>Executes PrepareReference as part of the FoImagePreparation conversion workflow.</para>
    /// <para>Выполняет PrepareReference в цепочке преобразования FoImagePreparation.</para>
    /// </summary>
    /// <param name="raw"><para>The raw argument.</para><para>Аргумент raw.</para></param>
    /// <param name="baseUri"><para>The baseUri argument.</para><para>Аргумент baseUri.</para></param>
    /// <param name="resources"><para>The resources argument.</para><para>Аргумент resources.</para></param>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <param name="budget"><para>The budget argument.</para><para>Аргумент budget.</para></param>
    /// <param name="depth"><para>The depth argument.</para><para>Аргумент depth.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    private string PrepareReference(string raw, Uri baseUri, LocalResourcePolicy resources, FopOptions options, Budget budget, int depth, CancellationToken token)
    {
        using var rawReader = new StreamReader(raw);
        char[] prefixBuffer = new char[256];
        int prefixLength = rawReader.Read(prefixBuffer, 0, prefixBuffer.Length);
        string prefix = new(prefixBuffer, 0, prefixLength);
        bool data = prefix.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase) || prefix.TrimStart().StartsWith("url(data:", StringComparison.OrdinalIgnoreCase) || prefix.TrimStart().StartsWith("url('data:", StringComparison.OrdinalIgnoreCase) || prefix.TrimStart().StartsWith("url(\"data:", StringComparison.OrdinalIgnoreCase);
        string path;
        if (data)
        {
            using FileStream rawFile = File.OpenRead(raw);
            string key = "data:" + Convert.ToHexString(SHA256.HashData(rawFile));
            if (budget.Images.TryGetValue(key, out string? cached))
            {
                return cached;
            }
            path = Path.Combine(directory, "decoded-" + Guid.NewGuid().ToString("N") + ".png");
            DecodeDataUri(raw, path, options.MaxImageBytes, budget, token);
            return PrepareImage(path, key, new Uri(path), resources, options, budget, depth, token);
        }
        if (new FileInfo(raw).Length > 16384)
        {
            throw new IOException("Local resource URI is too long.");
        }
        string value = UnwrapUrl(File.ReadAllText(raw));
        if (value.Length == 0 || value == "none")
        {
            return value;
        }
        Uri uri = new(baseUri, value);
        if (!uri.IsFile)
        {
            throw new NotSupportedException("Remote image resources are disabled.");
        }
        path = resources.Resolve(uri);
        // Hash the copied snapshot, not a file that FOP might reread after it changes.
        string snapshot = Path.Combine(directory, "source-" + Guid.NewGuid().ToString("N") + Path.GetExtension(path));
        using (FileStream input = File.OpenRead(path))
        using (var output = new BudgetStream(File.Create(snapshot), budget))
        {
            if (input.Length > options.MaxImageBytes)
            {
                throw new IOException("Image exceeds its byte limit.");
            }
            input.CopyTo(output);
        }
        string contentKey;
        using (FileStream hashFile = File.OpenRead(snapshot))
        {
            contentKey = uri.AbsoluteUri + ":" + Convert.ToHexString(SHA256.HashData(hashFile));
        }
        if (budget.Images.TryGetValue(contentKey, out string? replacement))
        {
            File.Delete(snapshot);
            return replacement;
        }
        return PrepareImage(snapshot, contentKey, uri, resources, options, budget, depth, token);
    }

    /// <summary>
    /// <para>Executes PrepareImage as part of the FoImagePreparation conversion workflow.</para>
    /// <para>Выполняет PrepareImage в цепочке преобразования FoImagePreparation.</para>
    /// </summary>
    /// <param name="snapshot"><para>The snapshot argument.</para><para>Аргумент snapshot.</para></param>
    /// <param name="key"><para>The key argument.</para><para>Аргумент key.</para></param>
    /// <param name="original"><para>The original argument.</para><para>Аргумент original.</para></param>
    /// <param name="resources"><para>The resources argument.</para><para>Аргумент resources.</para></param>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <param name="budget"><para>The budget argument.</para><para>Аргумент budget.</para></param>
    /// <param name="depth"><para>The depth argument.</para><para>Аргумент depth.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    private string PrepareImage(string snapshot, string key, Uri original, LocalResourcePolicy resources, FopOptions options, Budget budget, int depth, CancellationToken token)
    {
        using FileStream input = File.OpenRead(snapshot);
        budget.AddImage(input.Length);
        byte[] signature = new byte[8];
        int count = input.ReadAtLeast(signature, 8, false);
        input.Position = 0;
        string replacement;
        if (count == 8 && PngIccProfileRemover.IsPng(signature))
        {
            string cleaned = Path.Combine(directory, "image-" + budget.Count + ".png");
            using (var output = new BudgetStream(File.Create(cleaned), budget))
            {
                PngIccProfileRemover.RemoveIccProfile(input, output, options.MaxImageBytes, token);
            }
            replacement = new Uri(cleaned).AbsoluteUri;
        }
        else
        {
            if (Path.GetExtension(original.LocalPath).Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Invalid PNG signature.");
            }
            bool raster = count >= 3 && signature[0] == 255 && signature[1] == 216 && signature[2] == 255 || count >= 6 && (signature.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || signature.AsSpan(0, 6).SequenceEqual("GIF89a"u8));
            if (raster)
            {
                replacement = new Uri(snapshot).AbsoluteUri;
            }
            else
            {
                using var svg = XmlReader.Create(input, new()
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });
                svg.MoveToContent();
                if (svg.LocalName != "svg" || svg.NamespaceURI != SvgNamespace)
                {
                    throw new InvalidDataException("Unsupported image format.");
                }
                string rewritten = Path.Combine(directory, "svg-" + budget.Count + ".svg");
                Rewrite(snapshot, rewritten, original, resources, options, budget, depth + 1, token);
                replacement = new Uri(rewritten).AbsoluteUri;
            }
        }
        budget.Images.Add(key, replacement);
        return replacement;
    }

    /// <summary>
    /// <para>Executes DecodeDataUri as part of the FoImagePreparation conversion workflow.</para>
    /// <para>Выполняет DecodeDataUri в цепочке преобразования FoImagePreparation.</para>
    /// </summary>
    /// <param name="raw"><para>The raw argument.</para><para>Аргумент raw.</para></param>
    /// <param name="target"><para>The target argument.</para><para>Аргумент target.</para></param>
    /// <param name="limit"><para>The limit argument.</para><para>Аргумент limit.</para></param>
    /// <param name="budget"><para>The budget argument.</para><para>Аргумент budget.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    private static void DecodeDataUri(string raw, string target, long limit, Budget budget, CancellationToken token)
    {
        using var reader = new StreamReader(raw);
        var metadata = new StringBuilder();
        int value;
        while ((value = reader.Read()) != -1 && value != ',')
        {
            if (metadata.Length > 256)
            {
                throw new InvalidDataException("Invalid data URI.");
            }
            metadata.Append((char)value);
        }
        string header = metadata.ToString().Trim();
        int start = header.IndexOf("data:", StringComparison.OrdinalIgnoreCase);
        if (start < 0 || !header[start..].Split(';')[0].Equals("data:image/png", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Only PNG data URIs are supported.");
        }
        bool base64 = header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase);
        using var output = new BudgetStream(File.Create(target), budget);
        char[] quartet = new char[4];
        byte[] decoded = new byte[3];
        int filled = 0;
        long written = 0;
        bool ended = false;
        while ((value = reader.Read()) != -1)
        {
            token.ThrowIfCancellationRequested();
            char c = (char)value;
            bool escaped = c == '%';
            if (c == '%')
            {
                int a = reader.Read(), b = reader.Read();
                if (a < 0 || b < 0)
                {
                    throw new InvalidDataException("Invalid percent encoding.");
                }
                c = (char)Convert.ToByte(new string([(char)a, (char)b]), 16);
            }
            if (!escaped && c is '\'' or '"' or ')')
            {
                ended = true;
                continue;
            }
            if (base64 && char.IsWhiteSpace(c))
            {
                continue;
            }
            if (ended)
            {
                throw new InvalidDataException("Invalid trailing data URI content.");
            }
            if (!base64)
            {
                if (!escaped && c > 127 || ++written > limit)
                {
                    throw new IOException("Image exceeds its byte limit.");
                }
                output.WriteByte((byte)c);
            }
            else
            {
                quartet[filled++] = c;
                if (filled != 4)
                {
                    continue;
                }
                if (!Convert.TryFromBase64Chars(quartet, decoded, out int length))
                {
                    throw new InvalidDataException("Invalid base64 image.");
                }
                written += length;
                if (written > limit)
                {
                    throw new IOException("Image exceeds its byte limit.");
                }
                output.Write(decoded, 0, length);
                filled = 0;
            }
        }
        if (filled != 0)
        {
            throw new InvalidDataException("Truncated base64 image.");
        }
    }

    /// <summary>
    /// <para>Executes UnwrapUrl as part of the FoImagePreparation conversion workflow.</para>
    /// <para>Выполняет UnwrapUrl в цепочке преобразования FoImagePreparation.</para>
    /// </summary>
    /// <param name="value"><para>The value argument.</para><para>Аргумент value.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static string UnwrapUrl(string value)
    {
        value = value.Trim();
        if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(')'))
        {
            value = value[4..^1].Trim();
        }
        if (value.Length >= 2 && (value[0] == '\'' && value[^1] == '\'' || value[0] == '"' && value[^1] == '"'))
        {
            value = value[1..^1];
        }
        return value;
    }

    /// <summary>
    /// <para>Executes ValidateCss as part of the FoImagePreparation conversion workflow.</para>
    /// <para>Выполняет ValidateCss в цепочке преобразования FoImagePreparation.</para>
    /// </summary>
    /// <param name="value"><para>The value argument.</para><para>Аргумент value.</para></param>
    private static void ValidateCss(string value)
    {
        if (value.Contains('@') || value.Contains('\\') || value.Contains("/*", StringComparison.Ordinal))
        {
            throw new InvalidDataException("External SVG styles are disabled.");
        }
        if (Regex.IsMatch(value, @"\bfont(?:-family)?\s*:", RegexOptions.IgnoreCase))
        {
            throw new InvalidDataException("Specify SVG fonts with font-family attributes for deterministic profile resolution.");
        }
        foreach (Match match in Regex.Matches(value, @"url\s*\(([^)]*)\)", RegexOptions.IgnoreCase))
        {
            if (!match.Groups[1].Value.Trim().Trim('\'', '"').StartsWith('#'))
            {
                throw new InvalidDataException("Only SVG fragment style URLs are supported.");
            }
        }
    }

    /// <summary><para>Maps SVG families to explicitly registered fonts.</para><para>Сопоставляет семейства SVG с явно зарегистрированными шрифтами.</para></summary>
    /// <param name="value"><para>Declared family.</para><para>Заявленное семейство.</para></param>
    /// <param name="aliases"><para>Worker font aliases.</para><para>Псевдонимы шрифтов worker.</para></param>
    /// <returns><para>Registered physical family.</para><para>Зарегистрированное физическое семейство.</para></returns>
    private static string ResolveSvgFont(string value, IReadOnlyDictionary<string, string>? aliases)
    {
        string family = value.Split(',')[0].Trim().Trim('\'', '"');
        if (aliases is not null && aliases.TryGetValue(family, out string? physical))
        {
            return physical;
        }
        if (family.Equals("DejaVu Sans", StringComparison.OrdinalIgnoreCase) || family is "sans-serif" or "serif" or "monospace")
        {
            return "DejaVu Sans";
        }
        throw new InvalidDataException("SVG font is not registered in this profile; custom SVG fonts require a worker.");
    }

    /// <summary>
    /// <para>Releases owned resources; caller-owned input and output streams are not closed.</para>
    /// <para>Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются.</para>
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
