using Microsoft.Extensions.DependencyInjection;

namespace PdfGenerator;


/// <summary>
/// <para>Provides the ServiceCollectionExtensions contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию ServiceCollectionExtensions для преобразования PDF.</para>
/// </summary>
public static class ServiceCollectionExtensions
{

    /// <summary>
    /// <para>Executes AddXmlPdf as part of the ServiceCollectionExtensions conversion workflow.</para>
    /// <para>Выполняет AddXmlPdf в цепочке преобразования ServiceCollectionExtensions.</para>
    /// </summary>
    /// <param name="services"><para>The services argument.</para><para>Аргумент services.</para></param>
    /// <param name="profile"><para>The profile argument.</para><para>Аргумент profile.</para></param>
    /// <param name="mode"><para>The mode argument.</para><para>Аргумент mode.</para></param>
    /// <param name="workerOptions"><para>The workerOptions argument.</para><para>Аргумент workerOptions.</para></param>
    /// <param name="schedulerOptions"><para>The schedulerOptions argument.</para><para>Аргумент schedulerOptions.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public static IServiceCollection AddXmlPdf(this IServiceCollection services, PdfProcessingProfile profile, PdfExecutionMode mode = PdfExecutionMode.InProcess,
            PdfWorkerOptions? workerOptions = null, PdfSchedulerOptions? schedulerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return services.AddXmlPdf(mode, profile.RendererOptions, profile.XmlOptions, workerOptions,
            (schedulerOptions ?? new PdfSchedulerOptions { MaxActiveJobs = mode == PdfExecutionMode.Worker ? (workerOptions?.MaxWorkers ?? 1) : 2 }) with
            {
                ProfileVersion = profile.Version
            });
    }


    /// <summary>
    /// <para>Executes AddXmlPdf as part of the ServiceCollectionExtensions conversion workflow.</para>
    /// <para>Выполняет AddXmlPdf в цепочке преобразования ServiceCollectionExtensions.</para>
    /// </summary>
    /// <param name="services"><para>The services argument.</para><para>Аргумент services.</para></param>
    /// <param name="mode"><para>The mode argument.</para><para>Аргумент mode.</para></param>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <param name="xmlOptions"><para>The xmlOptions argument.</para><para>Аргумент xmlOptions.</para></param>
    /// <param name="workerOptions"><para>The workerOptions argument.</para><para>Аргумент workerOptions.</para></param>
    /// <param name="schedulerOptions"><para>The schedulerOptions argument.</para><para>Аргумент schedulerOptions.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public static IServiceCollection AddXmlPdf(this IServiceCollection services, PdfExecutionMode mode, EmbeddedFopOptions? options = null, XmlFoOptions? xmlOptions = null,
            PdfWorkerOptions? workerOptions = null, PdfSchedulerOptions? schedulerOptions = null)
    {
        if (mode == PdfExecutionMode.Worker)
        {
            PdfWorkerOptions worker = workerOptions ?? new();
            services.AddSingleton<IXmlPdfGenerator>(_ => new ScheduledXmlPdfGenerator(new WorkerXmlPdfGenerator(worker, xmlOptions, options),
                schedulerOptions ?? new PdfSchedulerOptions { MaxActiveJobs = worker.MaxWorkers }));
            services.AddSingleton<IXsltDiagnostics>(_ => new XsltDiagnostics(xmlOptions?.MaxStylesheetBytes ?? new XmlFoOptions().MaxStylesheetBytes));
            return services;
        }
        if (mode != PdfExecutionMode.InProcess)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        services.AddXmlFo(xmlOptions);
        services.AddSingleton<IFoPdfRenderer>(_ => new EmbeddedFopRenderer(options));
        services.AddSingleton<IXmlPdfGenerator>(provider => new ScheduledXmlPdfGenerator(
            new XmlPdfGenerator(provider.GetRequiredService<IXsltFoTransformer>(), provider.GetRequiredService<IFoPdfRenderer>()), schedulerOptions));
        return services;
    }


    /// <summary>
    /// <para>Executes AddXmlPdf as part of the ServiceCollectionExtensions conversion workflow.</para>
    /// <para>Выполняет AddXmlPdf в цепочке преобразования ServiceCollectionExtensions.</para>
    /// </summary>
    /// <param name="services"><para>The services argument.</para><para>Аргумент services.</para></param>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <param name="xmlOptions"><para>The xmlOptions argument.</para><para>Аргумент xmlOptions.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public static IServiceCollection AddXmlPdf(this IServiceCollection services, EmbeddedFopOptions? options = null, XmlFoOptions? xmlOptions = null)
    {
        return services.AddXmlPdf(PdfExecutionMode.InProcess, options, xmlOptions);
    }


    /// <summary>
    /// <para>Executes AddXmlFo as part of the ServiceCollectionExtensions conversion workflow.</para>
    /// <para>Выполняет AddXmlFo в цепочке преобразования ServiceCollectionExtensions.</para>
    /// </summary>
    /// <param name="services"><para>The services argument.</para><para>Аргумент services.</para></param>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public static IServiceCollection AddXmlFo(this IServiceCollection services, XmlFoOptions? options = null)
    {
        services.AddSingleton<IXsltFoTransformer>(_ => new SaxonFoTransformer(options));
        services.AddSingleton<IXsltDiagnostics>(_ => new XsltDiagnostics(options?.MaxStylesheetBytes ?? new XmlFoOptions().MaxStylesheetBytes));
        return services;
    }


    /// <summary>
    /// <para>Executes AddXmlPdf as part of the ServiceCollectionExtensions conversion workflow.</para>
    /// <para>Выполняет AddXmlPdf в цепочке преобразования ServiceCollectionExtensions.</para>
    /// </summary>
    /// <param name="services"><para>The services argument.</para><para>Аргумент services.</para></param>
    /// <param name="fopOptions"><para>The fopOptions argument.</para><para>Аргумент fopOptions.</para></param>
    /// <param name="xmlOptions"><para>The xmlOptions argument.</para><para>Аргумент xmlOptions.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public static IServiceCollection AddXmlPdf(this IServiceCollection services, FopOptions fopOptions, XmlFoOptions? xmlOptions = null)
    {
        services.AddXmlFo(xmlOptions);
        services.AddSingleton<IFoPdfRenderer>(_ => new FopProcessRenderer(fopOptions));
        services.AddSingleton<IXmlPdfGenerator>(provider => new ScheduledXmlPdfGenerator(
            new XmlPdfGenerator(provider.GetRequiredService<IXsltFoTransformer>(), provider.GetRequiredService<IFoPdfRenderer>())));
        return services;
    }
}
