using System.Diagnostics;

namespace PdfGenerator;


/// <summary>
/// <para>Provides the XmlPdfGenerator contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию XmlPdfGenerator для преобразования PDF.</para>
/// </summary>
/// <param name="transformer"><para>The transformer argument.</para><para>Аргумент transformer.</para></param>
/// <param name="renderer"><para>The renderer argument.</para><para>Аргумент renderer.</para></param>
public sealed class XmlPdfGenerator(IXsltFoTransformer transformer, IFoPdfRenderer renderer) : IXmlPdfGenerator
{

    /// <summary>
    /// <para>Converts XML and XSLT to PDF. File publication is atomic; copying a completed PDF to a caller-owned stream is not atomic and leaves that stream open.</para>
    /// <para>Преобразует XML и XSLT в PDF. Публикация файла атомарна; копирование готового PDF в поток вызывающего кода не атомарно и оставляет поток открытым.</para>
    /// </summary>
    /// <param name="xmlPath"><para>Local XML input path.</para><para>Путь локального входного XML.</para></param>
    /// <param name="xsltPath"><para>Local stylesheet path; relative dependencies require a permitted base directory.</para><para>Путь локального шаблона; относительным зависимостям требуется разрешённый базовый каталог.</para></param>
    /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public async Task GenerateFileAsync(string xmlPath, string xsltPath, string pdfPath, CancellationToken cancellationToken = default)
            => await GenerateAsync(new(XmlSource.FromFile(xmlPath), XmlSource.FromFile(xsltPath)), pdfPath, cancellationToken).ConfigureAwait(false);


    /// <summary>
    /// <para>Converts XML and XSLT to PDF. File publication is atomic; copying a completed PDF to a caller-owned stream is not atomic and leaves that stream open.</para>
    /// <para>Преобразует XML и XSLT в PDF. Публикация файла атомарна; копирование готового PDF в поток вызывающего кода не атомарно и оставляет поток открытым.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public Task<ConversionResult> GenerateAsync(ConversionRequest request, string pdfPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);
        string[] inputs = new[] { request.Xml.FilePath, request.Xslt.FilePath }.OfType<string>().ToArray();
        string temporary = AtomicOutput.Prepare(pdfPath, inputs);
        return GenerateCoreAsync(request, temporary, pdfPath, null, cancellationToken);
    }


    /// <summary>
    /// <para>Converts XML and XSLT to PDF. File publication is atomic; copying a completed PDF to a caller-owned stream is not atomic and leaves that stream open.</para>
    /// <para>Преобразует XML и XSLT в PDF. Публикация файла атомарна; копирование готового PDF в поток вызывающего кода не атомарно и оставляет поток открытым.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="output"><para>Output destination; caller-owned streams remain open and final copying is not atomic.</para><para>Выходное назначение; потоки вызывающего кода остаются открытыми, финальное копирование не атомарно.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public Task<ConversionResult> GenerateAsync(ConversionRequest request, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite || ReferenceEquals(output, request.Xml.Stream) || ReferenceEquals(output, request.Xslt.Stream))
        {
            throw new ArgumentException("Output must be writable and differ from input streams.", nameof(output));
        }
        string temporary = Path.Combine(Path.GetTempPath(), "xmlpdf-" + Guid.NewGuid().ToString("N") + ".pdf");
        return GenerateCoreAsync(request, temporary, null, output, cancellationToken);
    }


    /// <summary>
    /// <para>Executes GenerateCoreAsync as part of the XmlPdfGenerator conversion workflow.</para>
    /// <para>Выполняет GenerateCoreAsync в цепочке преобразования XmlPdfGenerator.</para>
    /// </summary>
    /// <param name="request"><para>Immutable job inputs and settings; its streams remain caller-owned.</para><para>Неизменяемые входы и настройки задания; его потоки принадлежат вызывающему коду.</para></param>
    /// <param name="temporary"><para>The temporary argument.</para><para>Аргумент temporary.</para></param>
    /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
    /// <param name="output"><para>Output destination; caller-owned streams remain open and final copying is not atomic.</para><para>Выходное назначение; потоки вызывающего кода остаются открытыми, финальное копирование не атомарно.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    private async Task<ConversionResult> GenerateCoreAsync(ConversionRequest request, string temporary, string? pdfPath, Stream? output, CancellationToken token)
    {
        var total = Stopwatch.StartNew();
        var watch = Stopwatch.StartNew();
        string fo = temporary + ".fo";
        ConversionStage stage = ConversionStage.Transform;
        try
        {
            token.ThrowIfCancellationRequested();
            TransformResult? transformation = null;
            if (transformer is IReportingXsltFoTransformer reportingTransformer)
            {
                transformation = await reportingTransformer.TransformWithResultAsync(request, fo, token).ConfigureAwait(false);
            }
            else if (transformer is IRequestXsltFoTransformer requestTransformer)
            {
                await requestTransformer.TransformAsync(request, fo, token).ConfigureAwait(false);
            }
            else if (request.Xml.FilePath is not null && request.Xslt.FilePath is not null && request.Parameters.Count == 0)
            {
                await transformer.TransformFileAsync(request.Xml.FilePath, request.Xslt.FilePath, fo, token).ConfigureAwait(false);
            }
            else
            {
                throw new NotSupportedException("The transformer does not support request inputs or parameters.");
            }
            TimeSpan transformTime = watch.Elapsed;
            watch.Restart();
            stage = ConversionStage.Render;
            RenderResult? report = null;
            if (renderer is IResourceFoPdfRenderer resourceRenderer)
            {
                Uri? baseUri = request.Xslt.BaseUri;
                LocalResourcePolicy policy = request.Resources ?? new LocalResourcePolicy(baseUri is null ? [] : [Path.GetDirectoryName(baseUri.LocalPath)!]);
                report = await resourceRenderer.RenderWithResourcesAsync(fo, temporary, baseUri, policy, token).ConfigureAwait(false);
            }
            else if (request.Resources is not null)
            {
                throw new NotSupportedException("The renderer does not support resource policies.");
            }
            else if (renderer is IReportingFoPdfRenderer reporting)
            {
                report = await reporting.RenderWithResultAsync(fo, temporary, token).ConfigureAwait(false);
            }
            else
            {
                await renderer.RenderFileAsync(fo, temporary, token).ConfigureAwait(false);
            }
            TimeSpan renderTime = watch.Elapsed;
            long size = new FileInfo(temporary).Length;
            watch.Restart();
            stage = ConversionStage.Publish;
            token.ThrowIfCancellationRequested();
            if (pdfPath is not null)
            {
                AtomicOutput.Publish(temporary, pdfPath, token);
            }
            else
            {
                await using FileStream source = File.OpenRead(temporary);
                await source.CopyToAsync(output!, token).ConfigureAwait(false);
            }
            return new(request.JobId, report?.PageCount, size, transformTime, renderTime, watch.Elapsed, total.Elapsed,
                report?.Warnings ?? Array.AsReadOnly(new[] { "Renderer does not provide page count or warnings." }))
            {
                Diagnostics = transformation?.Diagnostics ?? Array.Empty<XsltExecutionDiagnostic>(),
                InputPreparationDuration = transformation?.InputPreparation ?? TimeSpan.Zero,
                StageDurations = new System.Collections.ObjectModel.ReadOnlyDictionary<string, TimeSpan>(new Dictionary<string, TimeSpan>
                {
                    ["input"] = transformation?.InputPreparation ?? TimeSpan.Zero,
                    ["snapshot"] = transformation?.Snapshot ?? TimeSpan.Zero,
                    ["compile"] = transformation?.Compilation ?? TimeSpan.Zero,
                    ["transform"] = transformation?.Transformation ?? transformTime,
                    ["render"] = renderTime,
                    ["foPreparation"] = report?.PreparationDuration ?? TimeSpan.Zero,
                    ["factory"] = report?.FactoryDuration ?? TimeSpan.Zero,
                    ["fop"] = report?.FopDuration ?? TimeSpan.Zero,
                    ["publish"] = watch.Elapsed
                })
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (ConversionException) { throw; }
        catch (Exception ex) { throw new ConversionException(stage, $"XML → PDF failed during {stage}.", ex); }
        finally
        {
            if (File.Exists(fo))
            {
                File.Delete(fo);
            }
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
