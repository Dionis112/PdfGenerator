using System.Xml;
using System.Xml.Linq;

namespace Company.XmlFo;

internal sealed class FoImagePreparation : IDisposable
{
    private readonly string directory;
    public string FoPath
    {
        get;
    }

    private FoImagePreparation(string directory)
    {
        this.directory = directory;
        FoPath = Path.Combine(directory, "document.fo");
    }

    public static async Task<FoImagePreparation> CreateAsync(string foPath, string targetDirectory, FopOptions options, CancellationToken token)
    {
        var sourceUri = new Uri(Path.GetFullPath(foPath));
        if (new FileInfo(foPath).Length > options.MaxFoBytes)
        {
            throw new IOException("FO exceeds the input byte limit.");
        }

        using var reader = XmlReader.Create(foPath, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = options.MaxFoBytes
        });
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        var prepared = new FoImagePreparation(Path.Combine(targetDirectory, ".png-resources-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(prepared.directory);
        try
        {
            XNamespace fo = "http://www.w3.org/1999/XSL/Format";
            XNamespace svg = "http://www.w3.org/2000/svg";
            XNamespace xlink = "http://www.w3.org/1999/xlink";
            var attributes = document.Descendants().SelectMany(element =>
            {
                if (element.Name == fo + "external-graphic")
                {
                    return element.Attributes("src");
                }

                if (element.Name == svg + "image")
                {
                    return element.Attributes().Where(a => a.Name == "href" || a.Name == xlink + "href");
                }

                return Enumerable.Empty<XAttribute>();
            }).Concat(document.Descendants().Where(e => e.Name.Namespace == fo).Attributes("background-image")).ToList();
            var cache = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XAttribute? attribute in attributes)
            {
                token.ThrowIfCancellationRequested();
                string value = UnwrapUrl(attribute.Value);
                if (value.Length == 0 || value == "none")
                {
                    continue;
                }

                Uri baseUri = sourceUri;
                foreach (XElement? ancestor in attribute.Parent!.AncestorsAndSelf().Reverse())
                {
                    string? xmlBase = (string?)ancestor.Attribute(XNamespace.Xml + "base");
                    if (xmlBase is not null)
                    {
                        baseUri = new Uri(baseUri, xmlBase);
                    }
                }
                bool isData = value.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
                Uri? uri = isData ? null : new Uri(baseUri, value);
                string key = isData ? value : uri!.AbsoluteUri;
                if (!cache.TryGetValue(key, out string? replacement))
                {
                    byte[] bytes;
                    if (isData)
                    {
                        int comma = value.IndexOf(',');
                        if (comma < 0)
                        {
                            throw new InvalidDataException("Invalid image data URI.");
                        }

                        string metadata = value[..comma];
                        if (!metadata.Split(';')[0].Equals("data:image/png", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (value.Length - comma - 1 > options.MaxImageBytes * 4L)
                        {
                            throw new IOException("Embedded PNG exceeds the byte limit.");
                        }

                        string payload = value[(comma + 1)..];
                        bytes = metadata.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
                            ? Convert.FromBase64String(Uri.UnescapeDataString(payload)) : DecodePercentBytes(payload);
                    }
                    else if (uri!.IsFile)
                    {
                        await using FileStream image = File.OpenRead(uri.LocalPath);
                        byte[] signature = new byte[8];
                        int read = await image.ReadAtLeastAsync(signature, 8, throwOnEndOfStream: false, token).ConfigureAwait(false);
                        if (read != 8 || !PngIccProfileRemover.IsPng(signature))
                        {
                            if (Path.GetExtension(uri.LocalPath).Equals(".png", StringComparison.OrdinalIgnoreCase))
                            {
                                throw new InvalidDataException("PNG file has an invalid signature.");
                            }

                            attribute.Value = uri.AbsoluteUri;
                            continue;
                        }
                        if (image.Length > options.MaxImageBytes)
                        {
                            throw new IOException("PNG exceeds the byte limit.");
                        }

                        image.Position = 0;
                        bytes = new byte[checked((int)image.Length)];
                        await image.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
                    }
                    else
                    {
                        throw new NotSupportedException("Image preparation requires local files or embedded PNG data URIs; remote images must be downloaded by the caller.");
                    }

                    if (bytes.LongLength > options.MaxImageBytes)
                    {
                        throw new IOException("PNG exceeds the byte limit.");
                    }

                    byte[] cleaned = PngIccProfileRemover.RemoveIccProfile(bytes);
                    string imagePath = Path.Combine(prepared.directory, $"image-{cache.Count}.png");
                    await File.WriteAllBytesAsync(imagePath, cleaned, token).ConfigureAwait(false);
                    replacement = new Uri(imagePath).AbsoluteUri;
                    cache.Add(key, replacement);
                }
                attribute.Value = replacement;
            }
            XElement root = document.Root ?? throw new XmlException("FO has no root element.");
            root.SetAttributeValue(XNamespace.Xml + "base", new Uri(sourceUri, (string?)root.Attribute(XNamespace.Xml + "base") ?? "").AbsoluteUri);
            document.Save(prepared.FoPath, SaveOptions.DisableFormatting);
            token.ThrowIfCancellationRequested();
            return prepared;
        }
        catch
        {
            prepared.Dispose();
            throw;
        }
    }

    private static string UnwrapUrl(string value)
    {
        value = value.Trim();
        if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(')'))
        {
            value = value[4..^1].Trim();
        }

        if (value.Length >= 2 && ((value[0] == '\'' && value[^1] == '\'') || (value[0] == '"' && value[^1] == '"')))
        {
            value = value[1..^1];
        }

        return value;
    }

    private static byte[] DecodePercentBytes(string value)
    {
        using var bytes = new MemoryStream();
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '%' && i + 2 < value.Length)
            {
                bytes.WriteByte(Convert.ToByte(value.Substring(i + 1, 2), 16));
                i += 2;
            }
            else if (value[i] <= 127 && value[i] != '%')
            {
                bytes.WriteByte((byte)value[i]);
            }
            else
            {
                throw new InvalidDataException("Invalid percent-encoded PNG.");
            }
        }
        return bytes.ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
