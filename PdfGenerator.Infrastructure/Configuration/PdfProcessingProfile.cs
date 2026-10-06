using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace PdfGenerator;


/// <summary>
/// <para>Provides the PdfFont contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию PdfFont для преобразования PDF.</para>
/// </summary>
/// <param name="Path"><para>The Path argument.</para><para>Аргумент Path.</para></param>
/// <param name="Family"><para>The Family argument.</para><para>Аргумент Family.</para></param>
/// <param name="Style"><para>The Style argument.</para><para>Аргумент Style.</para></param>
/// <param name="Weight"><para>The Weight argument.</para><para>Аргумент Weight.</para></param>
public sealed record PdfFont(string Path, string Family, string Style = "normal", int Weight = 400);


/// <summary>
/// <para>Owns a versioned font/configuration snapshot; dispose only after all associated jobs and services finish.</para>
/// <para>Владеет версионированным снимком шрифтов и конфигурации; освобождать после завершения всех связанных заданий и сервисов.</para>
/// </summary>
public sealed class PdfProcessingProfile : IDisposable
{

    /// <summary>
    /// <para>Gets or defines the directory value used by PdfProcessingProfile.</para>
    /// <para>Возвращает или определяет значение directory, используемое PdfProcessingProfile.</para>
    /// </summary>
    private readonly string directory;

    /// <summary>
    /// <para>Gets or defines the Name value used by PdfProcessingProfile.</para>
    /// <para>Возвращает или определяет значение Name, используемое PdfProcessingProfile.</para>
    /// </summary>
    public string Name
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the Version value used by PdfProcessingProfile.</para>
    /// <para>Возвращает или определяет значение Version, используемое PdfProcessingProfile.</para>
    /// </summary>
    public string Version
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the XmlOptions value used by PdfProcessingProfile.</para>
    /// <para>Возвращает или определяет значение XmlOptions, используемое PdfProcessingProfile.</para>
    /// </summary>
    public XmlFoOptions XmlOptions
    {
        get;
    }

    /// <summary>
    /// <para>Gets or defines the RendererOptions value used by PdfProcessingProfile.</para>
    /// <para>Возвращает или определяет значение RendererOptions, используемое PdfProcessingProfile.</para>
    /// </summary>
    public EmbeddedFopOptions RendererOptions
    {
        get;
    }


    /// <summary>
    /// <para>Initializes PdfProcessingProfile with an immutable snapshot of service settings.</para>
    /// <para>Создаёт PdfProcessingProfile со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="name"><para>The name argument.</para><para>Аргумент name.</para></param>
    /// <param name="version"><para>The version argument.</para><para>Аргумент version.</para></param>
    /// <param name="directory"><para>The directory argument.</para><para>Аргумент directory.</para></param>
    /// <param name="xmlOptions"><para>The xmlOptions argument.</para><para>Аргумент xmlOptions.</para></param>
    /// <param name="rendererOptions"><para>The rendererOptions argument.</para><para>Аргумент rendererOptions.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    private PdfProcessingProfile(string name, string version, string directory, XmlFoOptions xmlOptions, EmbeddedFopOptions rendererOptions)
    {
        Name = name;
        Version = version;
        this.directory = directory;
        XmlOptions = xmlOptions;
        RendererOptions = rendererOptions;
    }


    /// <summary>
    /// <para>Executes Create as part of the PdfProcessingProfile conversion workflow.</para>
    /// <para>Выполняет Create в цепочке преобразования PdfProcessingProfile.</para>
    /// </summary>
    /// <param name="name"><para>The name argument.</para><para>Аргумент name.</para></param>
    /// <param name="fonts"><para>The fonts argument.</para><para>Аргумент fonts.</para></param>
    /// <param name="xmlOptions"><para>The xmlOptions argument.</para><para>Аргумент xmlOptions.</para></param>
    /// <param name="rendererOptions"><para>The rendererOptions argument.</para><para>Аргумент rendererOptions.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public static PdfProcessingProfile Create(string name, IEnumerable<PdfFont>? fonts = null,
            XmlFoOptions? xmlOptions = null, EmbeddedFopOptions? rendererOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        xmlOptions ??= new();
        rendererOptions ??= new();
        if (rendererOptions.ConfigurationPath is not null)
        {
            throw new ArgumentException("Prepared profiles generate their own configuration; register fonts explicitly.", nameof(rendererOptions));
        }
        // Reuse the engines' argument validation without starting any conversion.
        using var transformer = new SaxonFoTransformer(xmlOptions);
        using var renderer = new EmbeddedFopRenderer(rendererOptions);
        PdfFont[] faces = fonts?.ToArray() ?? [];
        if (faces.Length > 32 || faces.Any(f => f is null || string.IsNullOrWhiteSpace(f.Family) || f.Style is not ("normal" or "italic" or "oblique") || f.Weight < 100 || f.Weight > 900 || f.Weight % 100 != 0) ||
            faces.Select(f => (f.Family, f.Style, f.Weight)).Distinct().Count() != faces.Length)
        {
            throw new ArgumentException("Font faces must be valid and unique.", nameof(fonts));
        }
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xmlpdf-profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var config = XDocument.Load(BundledFopRuntime.ConfigurationPath);
            XElement fontList = config.Descendants("fonts").Single();
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long fontBytes = 0;
            hash.AppendData(Encoding.UTF8.GetBytes(typeof(PdfProcessingProfile).Assembly.FullName!));
            foreach (XAttribute bundledFont in fontList.Descendants("font").Attributes("embed-url"))
            {
                using FileStream bundledStream = File.OpenRead(new Uri(bundledFont.Value).LocalPath);
                fontBytes += bundledStream.Length;
                hash.AppendData(SHA256.HashData(bundledStream));
            }
            hash.AppendData(Encoding.UTF8.GetBytes(config.ToString().Replace(new Uri(System.IO.Path.GetDirectoryName(BundledFopRuntime.ConfigurationPath)! + System.IO.Path.DirectorySeparatorChar).AbsoluteUri, "bundled/", StringComparison.Ordinal)));
            hash.AppendData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(xmlOptions)));
            hash.AppendData(Encoding.UTF8.GetBytes(xmlOptions.Altova2012 is null ? "altova:disabled" : Altova2012Options.ImplementationVersion + ";optimizer:0;"));
            hash.AppendData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(rendererOptions)));
            for (int i = 0; i < faces.Length; i++)
            {
                PdfFont face = faces[i];
                string source = System.IO.Path.GetFullPath(face.Path);
                var policy = new LocalResourcePolicy([System.IO.Path.GetDirectoryName(source)!]);
                policy.Resolve(new Uri(source));
                if (new FileInfo(source).Length > 64 * 1024 * 1024)
                {
                    throw new IOException("Font exceeds its byte limit.");
                }
                fontBytes += new FileInfo(source).Length;
                if (fontBytes > rendererOptions.FactoryCacheBytes)
                {
                    throw new IOException("Profile fonts exceed their aggregate byte budget.");
                }
                string target = System.IO.Path.Combine(directory, $"font-{i}" + System.IO.Path.GetExtension(source));
                File.Copy(source, target);
                using (var validation = new java.io.FileInputStream(target))
                {
                    java.awt.Font.createFont(java.awt.Font.TRUETYPE_FONT, validation);
                }
                using FileStream stream = File.OpenRead(target);
                byte[] header = new byte[4];
                stream.ReadExactly(header);
                if (!header.AsSpan().SequenceEqual(new byte[] { 0, 1, 0, 0 }) && !header.AsSpan().SequenceEqual("OTTO"u8) && !header.AsSpan().SequenceEqual("ttcf"u8))
                {
                    throw new InvalidDataException("A registered font must be an OpenType or TrueType font.");
                }
                stream.Position = 0;
                hash.AppendData(SHA256.HashData(stream));
                hash.AppendData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new
                {
                    face.Family,
                    face.Style,
                    face.Weight
                })));
                fontList.Add(new XElement("font", new XAttribute("embed-url", new Uri(target).AbsoluteUri),
                    new XAttribute("embedding-mode", "subset"),
                    new XElement("font-triplet", new XAttribute("name", face.Family), new XAttribute("style", face.Style), new XAttribute("weight", face.Weight))));
            }
            string path = System.IO.Path.Combine(directory, "fop.xconf");
            config.Save(path);
            return new(name, Convert.ToHexString(hash.GetHashAndReset()), directory, xmlOptions,
                rendererOptions with
                {
                    ConfigurationPath = path
                });
        }
        catch { Directory.Delete(directory, true); throw; }
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


/// <summary>
/// <para>Provides the PdfExecutionMode contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию PdfExecutionMode для преобразования PDF.</para>
/// </summary>
public enum PdfExecutionMode
{

    /// <summary>
    /// <para>Gets or defines the InProcess value used by PdfExecutionMode.</para>
    /// <para>Возвращает или определяет значение InProcess, используемое PdfExecutionMode.</para>
    /// </summary>
    InProcess,
    /// <summary>
    /// <para>Gets or defines the Worker value used by PdfExecutionMode.</para>
    /// <para>Возвращает или определяет значение Worker, используемое PdfExecutionMode.</para>
    /// </summary>
    Worker
}
