using System.Xml;
using Saxon.Api;

namespace PdfGenerator;

/// <summary><para>Creates the same restricted Saxon configuration for runtime and validation.</para><para>Создаёт одинаковую ограниченную конфигурацию Saxon для приложения и стенда.</para></summary>
internal static class SaxonProcessorFactory
{
    /// <summary><para>Registers only the selected extension catalog while blocking external resources.</para><para>Регистрирует выбранный каталог расширений и запрещает внешние ресурсы.</para></summary>
    /// <param name="options"><para>Immutable XML configuration.</para><para>Неизменяемая конфигурация XML.</para></param>
    /// <returns><para>A configured processor.</para><para>Настроенный процессор.</para></returns>
    internal static Processor Create(XmlFoOptions options)
    {
        var processor = new Processor();
        processor.SetProperty(Feature.ALLOW_EXTERNAL_FUNCTIONS, false);
        processor.SetProperty(Feature.PRE_EVALUATE_DOC_FUNCTION, false);
        processor.CollectionFinder = _ => throw new XmlException("Collections are disabled.");
        processor.SetResourceResolver(_ => throw new XmlException("External runtime resources are disabled."), false);
        if (options.Altova2012 is not null)
        {
            options.Altova2012.Validate();
            // SaxonCS-HE 13.0.0 ignores API focus/side-effect flags, and its underlying methods are nonvirtual.
            // Disable optimizer rewrites for this opt-in profile until a fixed engine adapter is available.
            processor.SetProperty(Feature.OPTIMIZATION_LEVEL, "0");
            foreach (string name in AltovaFunctionCatalog.Names)
            {
                processor.RegisterExtensionFunction(new AltovaFunctionDefinition(name));
            }
        }
        return processor;
    }
}

