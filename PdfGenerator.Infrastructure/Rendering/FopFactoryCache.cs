using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace PdfGenerator;


/// <summary>
/// <para>Provides the FopCacheStatistics contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию FopCacheStatistics для преобразования PDF.</para>
/// </summary>
/// <param name="Hits"><para>The Hits argument.</para><para>Аргумент Hits.</para></param>
/// <param name="Misses"><para>The Misses argument.</para><para>Аргумент Misses.</para></param>
/// <param name="Evictions"><para>The Evictions argument.</para><para>Аргумент Evictions.</para></param>
/// <param name="Entries"><para>The Entries argument.</para><para>Аргумент Entries.</para></param>
/// <param name="EstimatedBytes"><para>Estimated retained bytes.</para><para>Оценочный объём сохраняемых данных.</para></param>
public sealed record FopCacheStatistics(long Hits, long Misses, long Evictions, int Entries, long EstimatedBytes);


/// <summary>
/// <para>Provides the FopFactoryCache contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию FopFactoryCache для преобразования PDF.</para>
/// </summary>
/// <param name="capacity"><para>The capacity argument.</para><para>Аргумент capacity.</para></param>
/// <param name="maximumBytes"><para>Estimated cache byte budget.</para><para>Оценочный бюджет байтов кэша.</para></param>
internal sealed class FopFactoryCache(int capacity, long maximumBytes) : IDisposable
{

    /// <summary>
    /// <para>Provides the Entry contract or implementation for PDF conversion.</para>
    /// <para>Предоставляет контракт или реализацию Entry для преобразования PDF.</para>
    /// </summary>
    /// <param name="directory"><para>The directory argument.</para><para>Аргумент directory.</para></param>
    /// <param name="factory"><para>The factory argument.</para><para>Аргумент factory.</para></param>
    /// <param name="weight"><para>Estimated retained bytes.</para><para>Оценочный объём сохраняемых данных.</para></param>
    private sealed class Entry(string directory, org.apache.fop.apps.FopFactory factory, long weight)
    {

        /// <summary>
        /// <para>Gets or defines the Directory value used by Entry.</para>
        /// <para>Возвращает или определяет значение Directory, используемое Entry.</para>
        /// </summary>
        public string Directory { get; } = directory;
        /// <summary><para>Estimated retained bytes.</para><para>Оценочный объём сохраняемых данных.</para></summary>
        public long Weight { get; } = weight;

        /// <summary>
        /// <para>Gets or defines the Factory value used by Entry.</para>
        /// <para>Возвращает или определяет значение Factory, используемое Entry.</para>
        /// </summary>
        public org.apache.fop.apps.FopFactory Factory { get; } = factory;

        /// <summary>
        /// <para>Gets or defines the Users value used by Entry.</para>
        /// <para>Возвращает или определяет значение Users, используемое Entry.</para>
        /// </summary>
        public int Users
        {
            get; set;
        }

        /// <summary>
        /// <para>Gets or defines the LastUse value used by Entry.</para>
        /// <para>Возвращает или определяет значение LastUse, используемое Entry.</para>
        /// </summary>
        public DateTime LastUse { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// <para>Provides the Lease contract or implementation for PDF conversion.</para>
    /// <para>Предоставляет контракт или реализацию Lease для преобразования PDF.</para>
    /// </summary>
    /// <param name="factory"><para>The factory argument.</para><para>Аргумент factory.</para></param>
    /// <param name="release"><para>The release argument.</para><para>Аргумент release.</para></param>
    internal sealed class Lease(org.apache.fop.apps.FopFactory factory, Action release) : IDisposable
    {

        /// <summary>
        /// <para>Gets or defines the Factory value used by Lease.</para>
        /// <para>Возвращает или определяет значение Factory, используемое Lease.</para>
        /// </summary>
        public org.apache.fop.apps.FopFactory Factory { get; } = factory;

        /// <summary>
        /// <para>Releases owned resources; caller-owned input and output streams are not closed.</para>
        /// <para>Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются.</para>
        /// </summary>
        public void Dispose() => release();
    }

    /// <summary>
    /// <para>Gets or defines the gate value used by FopFactoryCache.</para>
    /// <para>Возвращает или определяет значение gate, используемое FopFactoryCache.</para>
    /// </summary>
    private readonly object gate = new();

    /// <summary>
    /// <para>Gets or defines the entries value used by FopFactoryCache.</para>
    /// <para>Возвращает или определяет значение entries, используемое FopFactoryCache.</para>
    /// </summary>
    private readonly Dictionary<string, Entry> entries = new();

    /// <summary>
    /// <para>Gets or defines the hits value used by FopFactoryCache.</para>
    /// <para>Возвращает или определяет значение hits, используемое FopFactoryCache.</para>
    /// </summary>
    private long hits, misses, evictions;

    /// <summary>
    /// <para>Returns an immutable snapshot of cache or worker counters.</para>
    /// <para>Возвращает неизменяемый снимок счётчиков кэша или worker.</para>
    /// </summary>
    public FopCacheStatistics Statistics
    {
        get
        {
            lock (gate)
            {
                return new(hits, misses, evictions, entries.Count, entries.Values.Sum(e => e.Weight));
            }
        }
    }


    /// <summary>
    /// <para>Executes Acquire as part of the FopFactoryCache conversion workflow.</para>
    /// <para>Выполняет Acquire в цепочке преобразования FopFactoryCache.</para>
    /// </summary>
    /// <param name="configuration"><para>The configuration argument.</para><para>Аргумент configuration.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public Lease Acquire(string configuration)
    {
        string directory = Path.Combine(Path.GetTempPath(), "xmlpdf-factory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        bool retained = false;
        try
        {
            using var reader = System.Xml.XmlReader.Create(configuration, new()
            {
                DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 1024 * 1024
            });
            var document = XDocument.Load(reader);
            if (document.Descendants().Any(e => e.Name.LocalName is "auto-detect" or "directory"))
            {
                throw new InvalidDataException("Register explicit font files; host font discovery is disabled.");
            }
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(Encoding.UTF8.GetBytes(document.ToString()));
            Uri baseUri = new(Path.GetFullPath(configuration));
            string? configuredBase = document.Root?.Element("base")?.Value;
            if (configuredBase is not null)
            {
                baseUri = new Uri(baseUri, configuredBase);
            }
            int index = 0;
            long fontBytes = 0;
            foreach (XAttribute attribute in document.Descendants("font").Attributes().Where(a => a.Name.LocalName is "embed-url" or "metrics-url"))
            {
                Uri uri = new(baseUri, attribute.Value);
                if (!uri.IsFile || uri.IsUnc)
                {
                    throw new IOException("Font resources must be local files.");
                }
                string target = Path.Combine(directory, $"font-{index++}" + Path.GetExtension(uri.LocalPath));
                if (new FileInfo(uri.LocalPath).Length > 64 * 1024 * 1024)
                {
                    throw new IOException("Font is too large.");
                }
                File.Copy(uri.LocalPath, target);
                fontBytes += new FileInfo(target).Length;
                if (fontBytes > maximumBytes)
                {
                    throw new IOException("Font snapshot exceeds its cache byte budget.");
                }
                using FileStream file = File.OpenRead(target);
                hash.AppendData(SHA256.HashData(file));
                attribute.Value = new Uri(target).AbsoluteUri;
            }
            string key = Convert.ToHexString(hash.GetHashAndReset());
            long weight = checked(fontBytes * 2 + 4 * 1024 * 1024);
            lock (gate)
            {
                if (entries.TryGetValue(key, out Entry? existing))
                {
                    hits++;
                    existing.Users++;
                    existing.LastUse = DateTime.UtcNow;
                    return new(existing.Factory, () => { lock (gate) { existing.Users--; } });
                }
                misses++;
                while (entries.Count != 0 && (entries.Count >= capacity || entries.Values.Sum(e => e.Weight) + weight > maximumBytes))
                {
                    KeyValuePair<string, Entry> oldest = entries.Where(p => p.Value.Users == 0).OrderBy(p => p.Value.LastUse).FirstOrDefault();
                    if (oldest.Value is null)
                    {
                        break;
                    }
                    entries.Remove(oldest.Key);
                    System.IO.Directory.Delete(oldest.Value.Directory, true);
                    evictions++;
                }
                string snapshot = Path.Combine(directory, "fop.xconf");
                document.Save(snapshot);
                org.apache.fop.apps.FopFactory factory = new org.apache.fop.apps.FopConfParser(new java.io.File(snapshot)).getFopFactoryBuilder().build();
                // FOP lazily initializes its renderer configuration map. Initialize PDF configuration
                // before publishing the factory; concurrent documents then only read this shared map.
                using (var warmup = new java.io.ByteArrayOutputStream())
                {
                    factory.newFop("application/pdf", factory.newFOUserAgent(), warmup).getDefaultHandler();
                }
                if (entries.Count >= capacity || entries.Values.Sum(e => e.Weight) + weight > maximumBytes)
                {
                    retained = true;
                    return new(factory, () => System.IO.Directory.Delete(directory, true));
                }
                var entry = new Entry(directory, factory, weight) { Users = 1 };
                entries.Add(key, entry);
                retained = true;
                return new(factory, () => { lock (gate) { entry.Users--; } });
            }
        }
        finally { if (!retained) { Directory.Delete(directory, true); } }
    }


    /// <summary>
    /// <para>Releases owned resources; caller-owned input and output streams are not closed.</para>
    /// <para>Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются.</para>
    /// </summary>
    public void Dispose()
    {
        lock (gate)
        {
            foreach (Entry entry in entries.Values)
            {
                Directory.Delete(entry.Directory, true);
            }
            entries.Clear();
        }
    }
}
