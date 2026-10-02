using Microsoft.Extensions.DependencyInjection;

namespace Company.XmlFo;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddXmlPdf(this IServiceCollection services, EmbeddedFopOptions? options = null, XmlFoOptions? xmlOptions = null)
    {
        services.AddXmlFo(xmlOptions);
        services.AddSingleton<IFoPdfRenderer>(_ => new EmbeddedFopRenderer(options));
        services.AddSingleton<IXmlPdfGenerator, XmlPdfGenerator>();
        return services;
    }

    public static IServiceCollection AddXmlFo(this IServiceCollection services, XmlFoOptions? options = null)
    {
        services.AddSingleton<IXsltFoTransformer>(_ => new SaxonFoTransformer(options));
        return services;
    }

    public static IServiceCollection AddXmlPdf(this IServiceCollection services, FopOptions fopOptions, XmlFoOptions? xmlOptions = null)
    {
        services.AddXmlFo(xmlOptions);
        services.AddSingleton<IFoPdfRenderer>(_ => new FopProcessRenderer(fopOptions));
        services.AddSingleton<IXmlPdfGenerator, XmlPdfGenerator>();
        return services;
    }
}
