using System.Xml;
using Saxon.Api;

namespace Company.XmlFo;

/// <summary>Cancellation suppresses publication but cannot interrupt synchronous Saxon execution.</summary>
public sealed class SaxonFoTransformer : IXsltFoTransformer, IDisposable
{
    private readonly XmlFoOptions options;
    private readonly SemaphoreSlim slots;

    public SaxonFoTransformer(XmlFoOptions? options = null)
    {
        this.options = options ?? new();
        if (this.options.MaxConcurrency <= 0 || this.options.MaxInputBytes <= 0 || this.options.MaxOutputBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        slots = new(this.options.MaxConcurrency);
    }

    public async Task TransformFileAsync(string xmlPath, string xsltPath, string foPath, CancellationToken cancellationToken = default)
    {
        await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Do not abandon this task on cancellation: hold the slot until Saxon actually finishes.
            await Task.Run(() => Transform(xmlPath, xsltPath, foPath, cancellationToken), CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            slots.Release();
        }
    }

    private void Transform(string xmlPath, string xsltPath, string foPath, CancellationToken token)
    {
        ConversionStage stage = ConversionStage.Input;
        string? temporary = null;
        var readers = new List<XmlReader>();
        try
        {
            token.ThrowIfCancellationRequested();
            xmlPath = Path.GetFullPath(xmlPath);
            xsltPath = Path.GetFullPath(xsltPath);
            temporary = AtomicOutput.Prepare(foPath, xmlPath, xsltPath);
            var processor = new Processor();
            processor.SetProperty(Feature.ALLOW_EXTERNAL_FUNCTIONS, false);
            processor.SetProperty(Feature.PRE_EVALUATE_DOC_FUNCTION, false);
            processor.CollectionFinder = _ => throw new XmlException("External collections are disabled.");
            string root = Path.GetDirectoryName(xsltPath)!;
            processor.SetResourceResolver(request =>
            {
                token.ThrowIfCancellationRequested();
                if (request.Nature != ResourceRequest.XSLT_NATURE)
                {
                    throw new XmlException("External runtime resources are disabled.");
                }

                Uri uri = request.Uri;
                if (!uri.IsAbsoluteUri || !uri.IsFile || uri.IsUnc)
                {
                    throw new XmlException("Only local stylesheet modules are allowed.");
                }

                string path = Path.GetFullPath(uri.LocalPath);
                string relative = Path.GetRelativePath(root, path);
                if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar))
                {
                    throw new XmlException("Stylesheet module is outside the template directory.");
                }

                RejectLinks(path);
                XmlReader reader = OpenXml(path);
                readers.Add(reader);
                return new XmlReaderResource(processor.NewDocumentBuilder(), reader);
            }, false);
            stage = ConversionStage.Compile;
            using XmlReader stylesheet = OpenXml(xsltPath);
            XsltExecutable executable = processor.NewXsltCompiler().Compile(stylesheet);
            stage = ConversionStage.Transform;
            using XmlReader input = OpenXml(xmlPath);
            XsltTransformer transformer = executable.Load();
            transformer.InitialContextNode = processor.NewDocumentBuilder().Build(input);
            transformer.ResultDocumentHandler = (_, _) => throw new XmlException("Secondary output documents are disabled.");
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var limited = new LimitedWriteStream(output, options.MaxOutputBytes))
            {
                Serializer serializer = processor.NewSerializer(limited);
                serializer.SetOutputProperty(Serializer.METHOD, "xml");
                serializer.SetOutputProperty(Serializer.ENCODING, "utf-8");
                transformer.Run(serializer);
            }
            using (XmlReader result = OpenXml(temporary, options.MaxOutputBytes))
            {
                result.MoveToContent();
                if (result.LocalName != "root" || result.NamespaceURI != "http://www.w3.org/1999/XSL/Format")
                {
                    throw new XmlException("The stylesheet did not produce an XSL-FO root.");
                }

                while (result.Read())
                {
                }
            }
            stage = ConversionStage.Publish;
            AtomicOutput.Publish(temporary, foPath, token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ConversionException(stage, $"XML → FO failed during {stage}.", ex);
        }
        finally
        {
            foreach (XmlReader reader in readers)
            {
                reader.Dispose();
            }

            if (temporary is not null && File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private XmlReader OpenXml(string path, long? limit = null)
    {
        if (new FileInfo(path).Length > (limit ?? options.MaxInputBytes))
        {
            throw new IOException("XML resource exceeds the byte limit.");
        }

        return XmlReader.Create(path, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = limit ?? options.MaxInputBytes
        });
    }

    private static void RejectLinks(string path)
    {
        for (FileSystemInfo? item = new FileInfo(path); item is not null; item = item is FileInfo file ? file.Directory : ((DirectoryInfo)item).Parent)
        {
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new XmlException("Symbolic links in stylesheet module paths are disabled.");
            }
        }
    }

    public void Dispose() => slots.Dispose();
}

internal sealed class LimitedWriteStream(Stream inner, long limit) : Stream
{
    private long written;
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (written + buffer.Length > limit)
        {
            throw new IOException("FO output exceeds the byte limit.");
        }

        inner.Write(buffer);
        written += buffer.Length;
    }
    public override void Flush() => inner.Flush();
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => written;
    public override long Position
    {
        get => written; set => throw new NotSupportedException();
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
