using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Company.XmlFo;

internal static class BundledFopRuntime
{
    private const string FontResourcePrefix = ".Resources.Fonts.";
    private static readonly Lazy<string> Configuration = new(Initialize);

    public static string ConfigurationPath => Configuration.Value;

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
            AppDomain.CurrentDomain.ProcessExit += (_, _) => TryDeleteDirectory(directory);
            return configuration;
        }
        catch
        {
            TryDeleteDirectory(directory);
            throw;
        }
    }

    private static void ValidatePlatform()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64 ||
            (!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows()))
        {
            throw new PlatformNotSupportedException("Bundled FOP supports Linux glibc x64 and Windows x64.");
        }
    }

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

    [DllImport("libc", EntryPoint = "setenv", SetLastError = true)]
    private static extern int SetNativeEnvironment(string name, string value, int overwrite);
}
