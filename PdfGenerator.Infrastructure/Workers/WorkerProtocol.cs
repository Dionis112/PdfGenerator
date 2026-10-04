using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace PdfGenerator;


/// <summary>
/// <para>Provides the WorkerParameter contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию WorkerParameter для преобразования PDF.</para>
/// </summary>
/// <param name="LocalName"><para>The LocalName argument.</para><para>Аргумент LocalName.</para></param>
/// <param name="NamespaceUri"><para>The NamespaceUri argument.</para><para>Аргумент NamespaceUri.</para></param>
/// <param name="Type"><para>The Type argument.</para><para>Аргумент Type.</para></param>
/// <param name="Value"><para>The Value argument.</para><para>Аргумент Value.</para></param>
internal sealed record WorkerParameter(string LocalName, string NamespaceUri, string Type, string Value)
{

    /// <summary>
    /// <para>Executes ToParameter as part of the WorkerParameter conversion workflow.</para>
    /// <para>Выполняет ToParameter в цепочке преобразования WorkerParameter.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public XsltParameter ToParameter() => new(LocalName, Type switch
    {
        "string" => (object)Value,
        "boolean" => XmlConvert.ToBoolean(Value),
        "integer" => Value.StartsWith('-') ? (object)long.Parse(Value, CultureInfo.InvariantCulture) : ulong.Parse(Value, CultureInfo.InvariantCulture),
        "decimal" => XmlConvert.ToDecimal(Value),
        "dateTime" => XmlConvert.ToDateTimeOffset(Value),
        _ => throw new InvalidDataException("Unsupported parameter type.")
    }, NamespaceUri);
}

/// <summary>
/// <para>Provides the WorkerJob contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию WorkerJob для преобразования PDF.</para>
/// </summary>
/// <param name="Directory"><para>The Directory argument.</para><para>Аргумент Directory.</para></param>
/// <param name="JobId"><para>The JobId argument.</para><para>Аргумент JobId.</para></param>
/// <param name="XmlBaseUri"><para>The XmlBaseUri argument.</para><para>Аргумент XmlBaseUri.</para></param>
/// <param name="XsltBaseUri"><para>The XsltBaseUri argument.</para><para>Аргумент XsltBaseUri.</para></param>
/// <param name="ResourceRoots"><para>The ResourceRoots argument.</para><para>Аргумент ResourceRoots.</para></param>
/// <param name="Parameters"><para>The Parameters argument.</para><para>Аргумент Parameters.</para></param>
/// <param name="Settings"><para>The Settings argument.</para><para>Аргумент Settings.</para></param>
internal sealed record WorkerJob(string Directory, string JobId, string? XmlBaseUri, string? XsltBaseUri,
    string[] ResourceRoots, WorkerParameter[] Parameters, ConversionSettings Settings);

/// <summary>
/// <para>Provides the WorkerMessage contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию WorkerMessage для преобразования PDF.</para>
/// </summary>
/// <param name="Kind"><para>The Kind argument.</para><para>Аргумент Kind.</para></param>
/// <param name="Version"><para>The Version argument.</para><para>Аргумент Version.</para></param>
/// <param name="Engine"><para>The Engine argument.</para><para>Аргумент Engine.</para></param>
/// <param name="Job"><para>The Job argument.</para><para>Аргумент Job.</para></param>
/// <param name="Result"><para>The Result argument.</para><para>Аргумент Result.</para></param>
/// <param name="Stage"><para>The Stage argument.</para><para>Аргумент Stage.</para></param>
/// <param name="ErrorCode"><para>The ErrorCode argument.</para><para>Аргумент ErrorCode.</para></param>
internal sealed record WorkerMessage(string Kind, int Version = 1, string? Engine = null, WorkerJob? Job = null,
    ConversionResult? Result = null, ConversionStage? Stage = null, string? ErrorCode = null);


/// <summary>
/// <para>Provides the WorkerProtocol contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию WorkerProtocol для преобразования PDF.</para>
/// </summary>
internal static class WorkerProtocol
{

    /// <summary>
    /// <para>Gets or defines the Version value used by WorkerProtocol.</para>
    /// <para>Возвращает или определяет значение Version, используемое WorkerProtocol.</para>
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// <para>Gets or defines the MaxMessageCharacters value used by WorkerProtocol.</para>
    /// <para>Возвращает или определяет значение MaxMessageCharacters, используемое WorkerProtocol.</para>
    /// </summary>
    public const int MaxMessageCharacters = 1024 * 1024;

    /// <summary>
    /// <para>Gets or defines the Engine value used by WorkerProtocol.</para>
    /// <para>Возвращает или определяет значение Engine, используемое WorkerProtocol.</para>
    /// </summary>
    public static string Engine => typeof(Saxon.Api.Processor).Assembly.GetName().Version + ";FOP-2.11;" + typeof(WorkerProtocol).Assembly.GetName().Version;

    /// <summary>
    /// <para>Executes WriteAsync as part of the WorkerProtocol conversion workflow.</para>
    /// <para>Выполняет WriteAsync в цепочке преобразования WorkerProtocol.</para>
    /// </summary>
    /// <param name="writer"><para>The writer argument.</para><para>Аргумент writer.</para></param>
    /// <param name="message"><para>The message argument.</para><para>Аргумент message.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public static async Task WriteAsync(TextWriter writer, WorkerMessage message, CancellationToken token)
    {
        string json = JsonSerializer.Serialize(message);
        if (json.Length > MaxMessageCharacters)
        {
            throw new InvalidDataException("Worker message exceeds its limit.");
        }
        await writer.WriteLineAsync(json.AsMemory(), token).ConfigureAwait(false);
        await writer.FlushAsync(token).ConfigureAwait(false);
    }

    /// <summary>
    /// <para>Executes ReadAsync as part of the WorkerProtocol conversion workflow.</para>
    /// <para>Выполняет ReadAsync в цепочке преобразования WorkerProtocol.</para>
    /// </summary>
    /// <param name="reader"><para>The reader argument.</para><para>Аргумент reader.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public static async Task<WorkerMessage?> ReadAsync(TextReader reader, CancellationToken token)
    {
        var line = new StringBuilder();
        bool terminated = false;
        char[] buffer = new char[1];
        while (await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false) != 0)
        {
            if (buffer[0] == '\n')
            {
                terminated = true;
                break;
            }
            if (buffer[0] == '\r')
            {
                continue;
            }
            if (line.Length >= MaxMessageCharacters)
            {
                throw new InvalidDataException("Worker message exceeds its limit.");
            }
            line.Append(buffer[0]);
        }
        if (line.Length == 0)
        {
            if (terminated)
            {
                throw new InvalidDataException("Empty worker frame.");
            }
            return null;
        }
        if (!terminated)
        {
            throw new InvalidDataException("Truncated worker message.");
        }
        WorkerMessage message = JsonSerializer.Deserialize<WorkerMessage>(line.ToString()) ?? throw new InvalidDataException("Empty worker message.");
        if (message.Version != Version)
        {
            throw new InvalidDataException("Incompatible worker protocol version.");
        }
        return message;
    }
}
