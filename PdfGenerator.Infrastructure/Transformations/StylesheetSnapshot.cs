using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace PdfGenerator;


/// <summary>
/// <para>Provides the StylesheetSnapshot contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию StylesheetSnapshot для преобразования PDF.</para>
/// </summary>
internal sealed class StylesheetSnapshot : IDisposable
{

    /// <summary>
    /// <para>Gets or defines the directory value used by StylesheetSnapshot.</para>
    /// <para>Возвращает или определяет значение directory, используемое StylesheetSnapshot.</para>
    /// </summary>
    private readonly string directory = Path.Combine(Path.GetTempPath(), "xmlpdf-template-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// <para>Gets or defines the Modules value used by StylesheetSnapshot.</para>
    /// <para>Возвращает или определяет значение Modules, используемое StylesheetSnapshot.</para>
    /// </summary>
    public Dictionary<string, string> Modules { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// <para>Gets or defines the BaseUri value used by StylesheetSnapshot.</para>
    /// <para>Возвращает или определяет значение BaseUri, используемое StylesheetSnapshot.</para>
    /// </summary>
    public Uri BaseUri
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the Key value used by StylesheetSnapshot.</para>
    /// <para>Возвращает или определяет значение Key, используемое StylesheetSnapshot.</para>
    /// </summary>
    public string Key { get; private set; } = "";

    /// <summary>
    /// <para>Gets or defines the Bytes value used by StylesheetSnapshot.</para>
    /// <para>Возвращает или определяет значение Bytes, используемое StylesheetSnapshot.</para>
    /// </summary>
    public long Bytes
    {
        get; private set;
    }

    /// <summary>
    /// <para>Gets or defines the MainPath value used by StylesheetSnapshot.</para>
    /// <para>Возвращает или определяет значение MainPath, используемое StylesheetSnapshot.</para>
    /// </summary>
    public string MainPath => Modules[BaseUri.AbsoluteUri];


    /// <summary>
    /// <para>Initializes StylesheetSnapshot with an immutable snapshot of service settings.</para>
    /// <para>Создаёт StylesheetSnapshot со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="baseUri"><para>The baseUri argument.</para><para>Аргумент baseUri.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    private StylesheetSnapshot(Uri baseUri)
    {
        BaseUri = baseUri;
        Directory.CreateDirectory(directory);
    }


    /// <summary>
    /// <para>Executes Create as part of the StylesheetSnapshot conversion workflow.</para>
    /// <para>Выполняет Create в цепочке преобразования StylesheetSnapshot.</para>
    /// </summary>
    /// <param name="main"><para>The main argument.</para><para>Аргумент main.</para></param>
    /// <param name="resources"><para>The resources argument.</para><para>Аргумент resources.</para></param>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public static StylesheetSnapshot Create(PreparedXmlSource main, LocalResourcePolicy resources, XmlFoOptions options, CancellationToken token)
    {
        var snapshot = new StylesheetSnapshot(main.BaseUri ?? new Uri("urn:pdfgenerator:stream"));
        try
        {
            snapshot.Add(snapshot.BaseUri, main.Stream, resources, options.MaxStylesheetBytes, token);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(Encoding.UTF8.GetBytes(typeof(Saxon.Api.Processor).Assembly.FullName!));
            hash.AppendData("external-functions:false;pre-evaluate-doc:false;collections:false;runtime-resources:false;policy-v1"u8);
            hash.AppendData(Encoding.UTF8.GetBytes(snapshot.BaseUri.AbsoluteUri));
            foreach ((string uri, string path) in snapshot.Modules.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                hash.AppendData(Encoding.UTF8.GetBytes(uri + "\0"));
                using FileStream file = File.OpenRead(path);
                hash.AppendData(SHA256.HashData(file));
            }
            snapshot.Key = Convert.ToHexString(hash.GetHashAndReset());
            return snapshot;
        }
        catch { snapshot.Dispose(); throw; }
    }


    /// <summary>
    /// <para>Executes Add as part of the StylesheetSnapshot conversion workflow.</para>
    /// <para>Выполняет Add в цепочке преобразования StylesheetSnapshot.</para>
    /// </summary>
    /// <param name="uri"><para>The uri argument.</para><para>Аргумент uri.</para></param>
    /// <param name="source"><para>The source argument.</para><para>Аргумент source.</para></param>
    /// <param name="resources"><para>The resources argument.</para><para>Аргумент resources.</para></param>
    /// <param name="limit"><para>The limit argument.</para><para>Аргумент limit.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    private void Add(Uri uri, Stream source, LocalResourcePolicy resources, long limit, CancellationToken token)
    {
        if (Modules.ContainsKey(uri.AbsoluteUri))
        {
            return;
        }
        if (Modules.Count >= 256)
        {
            throw new IOException("Too many stylesheet modules.");
        }
        string path = Path.Combine(directory, $"{Modules.Count}.xsl");
        Modules.Add(uri.AbsoluteUri, path);
        using (FileStream output = File.Create(path))
        {
            byte[] buffer = new byte[65536];
            int count;
            while ((count = source.Read(buffer)) != 0)
            {
                token.ThrowIfCancellationRequested();
                if (count > limit - Bytes)
                {
                    throw new IOException("Stylesheet snapshot exceeds its byte limit.");
                }
                output.Write(buffer, 0, count);
                Bytes += count;
            }
        }
        using var reader = XmlReader.Create(File.OpenRead(path), new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            CloseInput = true,
            MaxCharactersInDocument = limit
        }, uri.AbsoluteUri);
        var bases = new Stack<Uri>();
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.EndElement)
            {
                bases.Pop();
            }
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }
            Uri current = bases.Count == 0 ? uri : bases.Peek();
            string? xmlBase = reader.GetAttribute("base", "http://www.w3.org/XML/1998/namespace");
            if (xmlBase is not null)
            {
                current = new Uri(current, xmlBase);
            }
            if (!reader.IsEmptyElement)
            {
                bases.Push(current);
            }
            if (reader.NamespaceURI != "http://www.w3.org/1999/XSL/Transform" || reader.LocalName is not ("include" or "import"))
            {
                continue;
            }
            string href = reader.GetAttribute("href") ?? throw new XmlException("Stylesheet include has no href.");
            if (mainIsAnonymous())
            {
                throw new XmlException("Stylesheet includes require a local base URI.");
            }
            Uri dependency = new(current, href);
            string dependencyPath = resources.Resolve(dependency);
            using FileStream input = File.OpenRead(dependencyPath);
            Add(dependency, input, resources, limit, token);
        }

        bool mainIsAnonymous() => !BaseUri.IsFile;
    }


    /// <summary>
    /// <para>Executes Open as part of the StylesheetSnapshot conversion workflow.</para>
    /// <para>Выполняет Open в цепочке преобразования StylesheetSnapshot.</para>
    /// </summary>
    /// <param name="uri"><para>The uri argument.</para><para>Аргумент uri.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public XmlReader Open(Uri uri) => XmlReader.Create(File.OpenRead(Modules[uri.AbsoluteUri]),
            new()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = true
            }, uri.AbsoluteUri);


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
