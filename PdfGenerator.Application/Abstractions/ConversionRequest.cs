using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml;

namespace PdfGenerator;


/// <summary>
/// <para>Describes a file or caller-owned stream, read from its current position without seeking or closing it.</para>
/// <para>Описывает файл или поток вызывающего кода, читаемый с текущей позиции без перемотки и закрытия.</para>
/// </summary>
public sealed class XmlSource
{

    /// <summary>
    /// <para>Gets or defines the FilePath value used by XmlSource.</para>
    /// <para>Возвращает или определяет значение FilePath, используемое XmlSource.</para>
    /// </summary>
    public string? FilePath
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the Stream value used by XmlSource.</para>
    /// <para>Возвращает или определяет значение Stream, используемое XmlSource.</para>
    /// </summary>
    public Stream? Stream
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the BaseUri value used by XmlSource.</para>
    /// <para>Возвращает или определяет значение BaseUri, используемое XmlSource.</para>
    /// </summary>
    public Uri? BaseUri
    {
        get;
    }


    /// <summary>
    /// <para>Initializes XmlSource with an immutable snapshot of service settings.</para>
    /// <para>Создаёт XmlSource со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="path"><para>The path argument.</para><para>Аргумент path.</para></param>
    /// <param name="stream"><para>The stream argument.</para><para>Аргумент stream.</para></param>
    /// <param name="baseUri"><para>The baseUri argument.</para><para>Аргумент baseUri.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    private XmlSource(string? path, Stream? stream, Uri? baseUri)
    {
        if (baseUri is not null && (!baseUri.IsAbsoluteUri || !baseUri.IsFile || baseUri.IsUnc))
        {
            throw new ArgumentException("Base URI must be an absolute local file URI.", nameof(baseUri));
        }
        FilePath = path;
        Stream = stream;
        BaseUri = baseUri;
    }


    /// <summary>
    /// <para>Executes FromFile as part of the XmlSource conversion workflow.</para>
    /// <para>Выполняет FromFile в цепочке преобразования XmlSource.</para>
    /// </summary>
    /// <param name="path"><para>The path argument.</para><para>Аргумент path.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public static XmlSource FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string absolute = Path.GetFullPath(path);
        return new(absolute, null, new Uri(absolute));
    }


    /// <summary>
    /// <para>Executes FromStream as part of the XmlSource conversion workflow.</para>
    /// <para>Выполняет FromStream в цепочке преобразования XmlSource.</para>
    /// </summary>
    /// <param name="stream"><para>The stream argument.</para><para>Аргумент stream.</para></param>
    /// <param name="baseUri"><para>The baseUri argument.</para><para>Аргумент baseUri.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public static XmlSource FromStream(Stream stream, Uri? baseUri = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }
        return new(null, stream, baseUri);
    }
}


/// <summary>
/// <para>Provides the XsltParameter contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию XsltParameter для преобразования PDF.</para>
/// </summary>
public sealed class XsltParameter
{

    /// <summary>
    /// <para>Gets or defines the Name value used by XsltParameter.</para>
    /// <para>Возвращает или определяет значение Name, используемое XsltParameter.</para>
    /// </summary>
    public XmlQualifiedName Name
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the Value value used by XsltParameter.</para>
    /// <para>Возвращает или определяет значение Value, используемое XsltParameter.</para>
    /// </summary>
    public object Value
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the Type value used by XsltParameter.</para>
    /// <para>Возвращает или определяет значение Type, используемое XsltParameter.</para>
    /// </summary>
    public string Type
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the LexicalValue value used by XsltParameter.</para>
    /// <para>Возвращает или определяет значение LexicalValue, используемое XsltParameter.</para>
    /// </summary>
    public string LexicalValue
    {
        get;
    }


    /// <summary>
    /// <para>Initializes XsltParameter with an immutable snapshot of service settings.</para>
    /// <para>Создаёт XsltParameter со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="localName"><para>The localName argument.</para><para>Аргумент localName.</para></param>
    /// <param name="value"><para>The value argument.</para><para>Аргумент value.</para></param>
    /// <param name="namespaceUri"><para>The namespaceUri argument.</para><para>Аргумент namespaceUri.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public XsltParameter(string localName, object value, string namespaceUri = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localName);
        XmlConvert.VerifyNCName(localName);
        ArgumentNullException.ThrowIfNull(namespaceUri);
        ArgumentNullException.ThrowIfNull(value);
        Name = new(localName, namespaceUri);
        Value = value;
        (Type, LexicalValue) = value switch
        {
            string s => ("string", s),
            bool b => ("boolean", XmlConvert.ToString(b)),
            sbyte or byte or short or ushort or int or uint or long or ulong => ("integer", Convert.ToString(value, CultureInfo.InvariantCulture)!),
            decimal d => ("decimal", XmlConvert.ToString(d)),
            DateTimeOffset d => ("dateTime", XmlConvert.ToString(d)),
            _ => throw new ArgumentException("Supported values: string, bool, integer, decimal, DateTimeOffset.", nameof(value))
        };
    }


    /// <summary>
    /// <para>Executes ToString as part of the XsltParameter conversion workflow.</para>
    /// <para>Выполняет ToString в цепочке преобразования XsltParameter.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public override string ToString() => $"{Name} ({Type})";
}


/// <summary>
/// <para>Captures immutable job metadata, qualified typed parameters, resource roots and per-call limits; input streams remain caller-owned.</para>
/// <para>Фиксирует неизменяемые данные задания, типизированные параметры QName, корни ресурсов и лимиты вызова; входные потоки принадлежат вызывающему коду.</para>
/// </summary>
public sealed class ConversionRequest
{

    /// <summary>
    /// <para>Gets or defines the Xml value used by ConversionRequest.</para>
    /// <para>Возвращает или определяет значение Xml, используемое ConversionRequest.</para>
    /// </summary>
    public XmlSource Xml
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the Xslt value used by ConversionRequest.</para>
    /// <para>Возвращает или определяет значение Xslt, используемое ConversionRequest.</para>
    /// </summary>
    public XmlSource Xslt
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the Parameters value used by ConversionRequest.</para>
    /// <para>Возвращает или определяет значение Parameters, используемое ConversionRequest.</para>
    /// </summary>
    public IReadOnlyList<XsltParameter> Parameters
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the JobId value used by ConversionRequest.</para>
    /// <para>Возвращает или определяет значение JobId, используемое ConversionRequest.</para>
    /// </summary>
    public string JobId
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the Resources value used by ConversionRequest.</para>
    /// <para>Возвращает или определяет значение Resources, используемое ConversionRequest.</para>
    /// </summary>
    public LocalResourcePolicy? Resources
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the Settings value used by ConversionRequest.</para>
    /// <para>Возвращает или определяет значение Settings, используемое ConversionRequest.</para>
    /// </summary>
    public ConversionSettings Settings
    {
        get;
    }


    /// <summary>
    /// <para>Initializes ConversionRequest with an immutable snapshot of service settings.</para>
    /// <para>Создаёт ConversionRequest со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="xml"><para>The xml argument.</para><para>Аргумент xml.</para></param>
    /// <param name="xslt"><para>The xslt argument.</para><para>Аргумент xslt.</para></param>
    /// <param name="parameters"><para>The parameters argument.</para><para>Аргумент parameters.</para></param>
    /// <param name="jobId"><para>The jobId argument.</para><para>Аргумент jobId.</para></param>
    /// <param name="resources"><para>The resources argument.</para><para>Аргумент resources.</para></param>
    /// <param name="settings"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public ConversionRequest(XmlSource xml, XmlSource xslt, IEnumerable<XsltParameter>? parameters = null, string? jobId = null, LocalResourcePolicy? resources = null, ConversionSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(xslt);
        Xml = xml;
        Xslt = xslt;
        Resources = resources;
        Settings = settings ?? new();
        if (Settings.Timeout <= TimeSpan.Zero || Settings.MaxInputBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings));
        }
        XsltParameter[] snapshot = parameters?.ToArray() ?? [];
        if (snapshot.Any(p => p is null) || snapshot.Select(p => p.Name).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("Parameters must have unique qualified names and non-null values.", nameof(parameters));
        }
        Parameters = new ReadOnlyCollection<XsltParameter>(snapshot);
        JobId = jobId ?? Guid.NewGuid().ToString("N");
        ArgumentException.ThrowIfNullOrWhiteSpace(JobId);
        if (xml.Stream is not null && ReferenceEquals(xml.Stream, xslt.Stream))
        {
            throw new ArgumentException("XML and XSLT must use separate streams.");
        }
    }
}


/// <summary>
/// <para>Provides the ConversionResult contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию ConversionResult для преобразования PDF.</para>
/// </summary>
/// <param name="JobId"><para>The JobId argument.</para><para>Аргумент JobId.</para></param>
/// <param name="PageCount"><para>The PageCount argument.</para><para>Аргумент PageCount.</para></param>
/// <param name="PdfBytes"><para>The PdfBytes argument.</para><para>Аргумент PdfBytes.</para></param>
/// <param name="TransformDuration"><para>The TransformDuration argument.</para><para>Аргумент TransformDuration.</para></param>
/// <param name="RenderDuration"><para>The RenderDuration argument.</para><para>Аргумент RenderDuration.</para></param>
/// <param name="PublishDuration"><para>The PublishDuration argument.</para><para>Аргумент PublishDuration.</para></param>
/// <param name="TotalDuration"><para>The TotalDuration argument.</para><para>Аргумент TotalDuration.</para></param>
/// <param name="Warnings"><para>The Warnings argument.</para><para>Аргумент Warnings.</para></param>
public sealed record ConversionResult(string JobId, int? PageCount, long PdfBytes,
    TimeSpan TransformDuration, TimeSpan RenderDuration, TimeSpan PublishDuration,
    TimeSpan TotalDuration, IReadOnlyList<string> Warnings)
{

    /// <summary>
    /// <para>Gets or defines the QueueDuration value used by ConversionResult.</para>
    /// <para>Возвращает или определяет значение QueueDuration, используемое ConversionResult.</para>
    /// </summary>
    public TimeSpan QueueDuration
    {
        get; init;
    }

    /// <summary>
    /// <para>Gets or defines the InputPreparationDuration value used by ConversionResult.</para>
    /// <para>Возвращает или определяет значение InputPreparationDuration, используемое ConversionResult.</para>
    /// </summary>
    public TimeSpan InputPreparationDuration
    {
        get; init;
    }

    /// <summary>
    /// <para>Gets or defines the ProfileVersion value used by ConversionResult.</para>
    /// <para>Возвращает или определяет значение ProfileVersion, используемое ConversionResult.</para>
    /// </summary>
    public string? ProfileVersion
    {
        get; init;
    }

    /// <summary>
    /// <para>Gets or defines the StageDurations value used by ConversionResult.</para>
    /// <para>Возвращает или определяет значение StageDurations, используемое ConversionResult.</para>
    /// </summary>
    public IReadOnlyDictionary<string, TimeSpan> StageDurations { get; init; } = new ReadOnlyDictionary<string, TimeSpan>(new Dictionary<string, TimeSpan>());
    /// <summary>
    /// <para>Optional engine counters at job completion, including worker-local cache and GC statistics; these are observations, not hard limits.</para>
    /// <para>Необязательные счётчики движка после задания, включая кэши и GC worker; это наблюдения, а не строгие лимиты.</para>
    /// </summary>
    public IReadOnlyDictionary<string, long> EngineCounters { get; init; } = new ReadOnlyDictionary<string, long>(new Dictionary<string, long>());
}


/// <summary>
/// <para>Provides the IReportingFoPdfRenderer contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию IReportingFoPdfRenderer для преобразования PDF.</para>
/// </summary>
public interface IReportingFoPdfRenderer : IFoPdfRenderer
{

    /// <summary>
    /// <para>Prepares local image snapshots, renders a complete PDF and checks cancellation before atomic file publication.</para>
    /// <para>Подготавливает локальные снимки изображений, рендерит готовый PDF и проверяет отмену перед атомарной публикацией файла.</para>
    /// </summary>
    /// <param name="foPath"><para>The foPath argument.</para><para>Аргумент foPath.</para></param>
    /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    Task<RenderResult> RenderWithResultAsync(string foPath, string pdfPath, CancellationToken cancellationToken = default);
}


/// <summary>
/// <para>Provides the RenderResult contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию RenderResult для преобразования PDF.</para>
/// </summary>
/// <param name="PageCount"><para>The PageCount argument.</para><para>Аргумент PageCount.</para></param>
/// <param name="Warnings"><para>The Warnings argument.</para><para>Аргумент Warnings.</para></param>
public sealed record RenderResult(int PageCount, IReadOnlyList<string> Warnings)
{
    /// <summary>
    /// <para>Time spent streaming FO and preparing local image snapshots.</para>
    /// <para>Время последовательной подготовки FO и локальных снимков изображений.</para>
    /// </summary>
    public TimeSpan PreparationDuration
    {
        get; init;
    }
    /// <summary>
    /// <para>Time spent acquiring or constructing the FOP factory.</para>
    /// <para>Время получения или построения фабрики FOP.</para>
    /// </summary>
    public TimeSpan FactoryDuration
    {
        get; init;
    }
    /// <summary>
    /// <para>Time spent executing the FOP layout and PDF serialization.</para>
    /// <para>Время выполнения размещения FOP и сериализации PDF.</para>
    /// </summary>
    public TimeSpan FopDuration
    {
        get; init;
    }
}


/// <summary>
/// <para>Provides the TransformResult contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию TransformResult для преобразования PDF.</para>
/// </summary>
/// <param name="InputPreparation"><para>The InputPreparation argument.</para><para>Аргумент InputPreparation.</para></param>
/// <param name="Snapshot"><para>The Snapshot argument.</para><para>Аргумент Snapshot.</para></param>
/// <param name="Compilation"><para>The Compilation argument.</para><para>Аргумент Compilation.</para></param>
/// <param name="Transformation"><para>The Transformation argument.</para><para>Аргумент Transformation.</para></param>
public sealed record TransformResult(TimeSpan InputPreparation, TimeSpan Snapshot, TimeSpan Compilation, TimeSpan Transformation);


/// <summary>
/// <para>Provides the IReportingXsltFoTransformer contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию IReportingXsltFoTransformer для преобразования PDF.</para>
/// </summary>
public interface IReportingXsltFoTransformer : IRequestXsltFoTransformer
{

    /// <summary>
    /// <para>Prepares bounded input snapshots and transforms with job-local parameters; in-process cancellation is observed around synchronous Saxon execution.</para>
    /// <para>Подготавливает ограниченные снимки входов и преобразует с параметрами задания; отмена внутри процесса проверяется вокруг синхронного выполнения Saxon.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="foPath"><para>The foPath argument.</para><para>Аргумент foPath.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    Task<TransformResult> TransformWithResultAsync(ConversionRequest request, string foPath, CancellationToken cancellationToken = default);
}


/// <summary>
/// <para>Provides the IResourceFoPdfRenderer contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию IResourceFoPdfRenderer для преобразования PDF.</para>
/// </summary>
public interface IResourceFoPdfRenderer : IReportingFoPdfRenderer
{

    /// <summary>
    /// <para>Prepares local image snapshots, renders a complete PDF and checks cancellation before atomic file publication.</para>
    /// <para>Подготавливает локальные снимки изображений, рендерит готовый PDF и проверяет отмену перед атомарной публикацией файла.</para>
    /// </summary>
    /// <param name="foPath"><para>The foPath argument.</para><para>Аргумент foPath.</para></param>
    /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
    /// <param name="baseUri"><para>The baseUri argument.</para><para>Аргумент baseUri.</para></param>
    /// <param name="resources"><para>The resources argument.</para><para>Аргумент resources.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    Task<RenderResult> RenderWithResourcesAsync(string foPath, string pdfPath, Uri? baseUri,
            LocalResourcePolicy resources, CancellationToken cancellationToken = default);
}


/// <summary>
/// <para>Provides the IRequestXsltFoTransformer contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию IRequestXsltFoTransformer для преобразования PDF.</para>
/// </summary>
public interface IRequestXsltFoTransformer : IXsltFoTransformer
{

    /// <summary>
    /// <para>Prepares bounded input snapshots and transforms with job-local parameters; in-process cancellation is observed around synchronous Saxon execution.</para>
    /// <para>Подготавливает ограниченные снимки входов и преобразует с параметрами задания; отмена внутри процесса проверяется вокруг синхронного выполнения Saxon.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="foPath"><para>The foPath argument.</para><para>Аргумент foPath.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    Task TransformAsync(ConversionRequest request, string foPath, CancellationToken cancellationToken = default);
}
