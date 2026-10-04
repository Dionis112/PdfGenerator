using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace PdfGenerator;


/// <summary>
/// <para>Provides the BundledFopRuntime contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию BundledFopRuntime для преобразования PDF.</para>
/// </summary>
internal static class BundledFopRuntime
{

    /// <summary>
    /// <para>Gets or defines the FontResourcePrefix value used by BundledFopRuntime.</para>
    /// <para>Возвращает или определяет значение FontResourcePrefix, используемое BundledFopRuntime.</para>
    /// </summary>
    private const string FontResourcePrefix = ".Resources.Fonts.";

    /// <summary>
    /// <para>Gets or defines the Configuration value used by BundledFopRuntime.</para>
    /// <para>Возвращает или определяет значение Configuration, используемое BundledFopRuntime.</para>
    /// </summary>
    private static readonly Lazy<string> Configuration = new(Initialize);


    /// <summary>
    /// <para>Gets or defines the ConfigurationPath value used by BundledFopRuntime.</para>
    /// <para>Возвращает или определяет значение ConfigurationPath, используемое BundledFopRuntime.</para>
    /// </summary>
    public static string ConfigurationPath => Configuration.Value;


    /// <summary>
    /// <para>Executes Initialize as part of the BundledFopRuntime conversion workflow.</para>
    /// <para>Выполняет Initialize в цепочке преобразования BundledFopRuntime.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static string Initialize()
    {
        ValidatePlatform();
        string directory = Path.Combine(Path.GetTempPath(), "xmlfo-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            ExtractFonts(directory);
            if (OperatingSystem.IsLinux())
            {
                ConfigureLinuxFonts(directory);
            }

            string configuration = WriteFopConfiguration(directory);
            RegisterFonts(configuration);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => TryDeleteDirectory(directory);
            return configuration;
        }
        catch
        {
            TryDeleteDirectory(directory);
            throw;
        }
    }


    /// <summary>
    /// <para>Executes ValidatePlatform as part of the BundledFopRuntime conversion workflow.</para>
    /// <para>Выполняет ValidatePlatform в цепочке преобразования BundledFopRuntime.</para>
    /// </summary>
    private static void ValidatePlatform()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64 ||
            (!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows()))
        {
            throw new PlatformNotSupportedException("Bundled FOP supports Linux glibc x64 and Windows x64.");
        }
    }


    /// <summary>
    /// <para>Executes ExtractFonts as part of the BundledFopRuntime conversion workflow.</para>
    /// <para>Выполняет ExtractFonts в цепочке преобразования BundledFopRuntime.</para>
    /// </summary>
    /// <param name="directory"><para>The directory argument.</para><para>Аргумент directory.</para></param>
    private static void ExtractFonts(string directory)
    {
        System.Reflection.Assembly assembly = typeof(BundledFopRuntime).Assembly;
        foreach (string? name in assembly.GetManifestResourceNames().Where(n => n.Contains(FontResourcePrefix, StringComparison.Ordinal)))
        {
            string file = name[(name.IndexOf(FontResourcePrefix, StringComparison.Ordinal) + FontResourcePrefix.Length)..];
            using Stream input = assembly.GetManifestResourceStream(name)!;
            using FileStream output = File.Create(Path.Combine(directory, file));
            input.CopyTo(output);
        }
    }


    /// <summary>
    /// <para>Executes ConfigureLinuxFonts as part of the BundledFopRuntime conversion workflow.</para>
    /// <para>Выполняет ConfigureLinuxFonts в цепочке преобразования BundledFopRuntime.</para>
    /// </summary>
    /// <param name="directory"><para>The directory argument.</para><para>Аргумент directory.</para></param>
    private static void ConfigureLinuxFonts(string directory)
    {
        // Dependencies are loaded first, without LD_LIBRARY_PATH or Docker ENV settings.
        string[] libraries = ["libbrotlicommon.so.1", "libbrotlidec.so.1", "libpng16.so.16", "libexpat.so.1", "libfreetype.so.6", "libfontconfig.so.1"];
        foreach (string library in libraries)
        {
            string native = Path.Combine(AppContext.BaseDirectory, library);
            if (!File.Exists(native))
            {
                native = Path.Combine(AppContext.BaseDirectory, "runtimes", "linux-x64", "native", library);
            }

            NativeLibrary.Load(native);
        }

        string fontConfig = Path.Combine(directory, "fonts.conf");
        string[] families = ["sans-serif", "serif", "monospace"];
        var configuration = new XElement("fontconfig",
            new XElement("dir", directory),
            new XElement("cachedir", Path.Combine(directory, "cache")),
            families.Select(family => new XElement("alias",
                new XElement("family", family),
                new XElement("prefer", new XElement("family", "DejaVu Sans")))));
        new XDocument(configuration).Save(fontConfig);

        // Native Fontconfig reads libc's environment, not .NET's managed environment cache.
        if (SetNativeEnvironment("FONTCONFIG_FILE", fontConfig, 1) != 0)
        {
            throw new IOException("Cannot configure bundled fonts.");
        }
    }


    /// <summary>
    /// <para>Executes WriteFopConfiguration as part of the BundledFopRuntime conversion workflow.</para>
    /// <para>Выполняет WriteFopConfiguration в цепочке преобразования BundledFopRuntime.</para>
    /// </summary>
    /// <param name="directory"><para>The directory argument.</para><para>Аргумент directory.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static string WriteFopConfiguration(string directory)
    {
        string fontUri = new Uri(Path.Combine(directory, "DejaVuSans.ttf")).AbsoluteUri;
        string[] families = ["sans-serif", "serif", "monospace", "DejaVu Sans"];
        var font = new XElement("font",
            new XAttribute("embed-url", fontUri),
            new XAttribute("embedding-mode", "subset"),
            families.Select(family => new XElement("font-triplet",
                new XAttribute("name", family),
                new XAttribute("style", "normal"),
                new XAttribute("weight", "normal"))));
        var configuration = new XElement("fop",
            new XAttribute("version", "1.0"),
            new XElement("strict-validation", "true"),
            new XElement("use-cache", "false"),
            new XElement("renderers",
                new XElement("renderer", new XAttribute("mime", "application/pdf"),
                    new XElement("fonts", font))));
        string path = Path.Combine(directory, "fop.xconf");
        new XDocument(configuration).Save(path);
        return path;
    }


    /// <summary>
    /// <para>Executes RegisterFonts as part of the BundledFopRuntime conversion workflow.</para>
    /// <para>Выполняет RegisterFonts в цепочке преобразования BundledFopRuntime.</para>
    /// </summary>
    /// <param name="configuration"><para>The configuration argument.</para><para>Аргумент configuration.</para></param>
    /// <returns><para>Configured aliases mapped to registered physical font families.</para><para>Настроенные псевдонимы, сопоставленные с зарегистрированными семействами шрифтов.</para></returns>
    internal static IReadOnlyDictionary<string, string> RegisterFonts(string configuration)
    {
        java.lang.System.setProperty("java.awt.headless", "true");
        using var reader = System.Xml.XmlReader.Create(configuration, new()
        {
            DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 1024 * 1024
        });
        var document = XDocument.Load(reader);
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Uri baseUri = new(Path.GetFullPath(configuration));
        if (document.Root?.Element("base") is { } configuredBase)
        {
            baseUri = new Uri(baseUri, configuredBase.Value);
        }
        foreach (XElement element in document.Descendants("font"))
        {
            if (element.Attribute("embed-url") is not { } embedded)
            {
                continue;
            }
            Uri uri = new(baseUri, embedded.Value);
            if (!uri.IsFile || uri.IsUnc)
            {
                throw new IOException("Font resources must be local.");
            }
            string path = uri.LocalPath;
            using var input = new java.io.FileInputStream(path);
            var font = java.awt.Font.createFont(java.awt.Font.TRUETYPE_FONT, input);
            java.awt.GraphicsEnvironment.getLocalGraphicsEnvironment().registerFont(font);
            string family = font.getFamily();
            aliases[family] = family;
            foreach (XAttribute alias in element.Elements("font-triplet").Attributes("name"))
            {
                aliases[alias.Value] = family;
            }
        }
        return aliases;
    }


    /// <summary>
    /// <para>Executes TryDeleteDirectory as part of the BundledFopRuntime conversion workflow.</para>
    /// <para>Выполняет TryDeleteDirectory в цепочке преобразования BundledFopRuntime.</para>
    /// </summary>
    /// <param name="directory"><para>The directory argument.</para><para>Аргумент directory.</para></param>
    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Open native/font handles can outlive process shutdown callbacks.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup must not hide the original conversion failure.
        }
    }


    /// <summary>
    /// <para>Executes SetNativeEnvironment as part of the BundledFopRuntime conversion workflow.</para>
    /// <para>Выполняет SetNativeEnvironment в цепочке преобразования BundledFopRuntime.</para>
    /// </summary>
    /// <param name="name"><para>The name argument.</para><para>Аргумент name.</para></param>
    /// <param name="value"><para>The value argument.</para><para>Аргумент value.</para></param>
    /// <param name="overwrite"><para>The overwrite argument.</para><para>Аргумент overwrite.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [DllImport("libc", EntryPoint = "setenv", SetLastError = true)]
    private static extern int SetNativeEnvironment(string name, string value, int overwrite);
}
