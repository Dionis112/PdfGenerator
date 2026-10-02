namespace Company.XmlFo;

/// <summary>Renders FO with bundled Apache FOP through IKVM; no Java installation is required.</summary>
public sealed class EmbeddedFopRenderer : IFoPdfRenderer, IDisposable
{
    private readonly EmbeddedFopOptions options;
    private readonly SemaphoreSlim slots;

    public EmbeddedFopRenderer(EmbeddedFopOptions? options = null)
    {
        this.options = options ?? new();
        if (this.options.MaxConcurrency <= 0 || this.options.MaxPdfBytes <= 0 || this.options.MaxFoBytes <= 0 || this.options.MaxImageBytes <= 0 || this.options.MaxImageBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        slots = new(this.options.MaxConcurrency);
    }

    public async Task RenderFileAsync(string foPath, string pdfPath, CancellationToken cancellationToken = default)
    {
        await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporary = null;
        ConversionStage stage = ConversionStage.Render;
        try
        {
            temporary = AtomicOutput.Prepare(pdfPath, foPath);
            if (!File.Exists(foPath))
            {
                throw new FileNotFoundException("FO input not found.", foPath);
            }

            using FoImagePreparation images = await FoImagePreparation.CreateAsync(foPath, Path.GetDirectoryName(temporary)!,
                new FopOptions { MaxFoBytes = options.MaxFoBytes, MaxImageBytes = options.MaxImageBytes }, cancellationToken).ConfigureAwait(false);
            string bundledConfig = BundledFopRuntime.ConfigurationPath;
            string configuration = options.ConfigurationPath is null ? bundledConfig : Path.GetFullPath(options.ConfigurationPath);
            cancellationToken.ThrowIfCancellationRequested();
            // FOP is synchronous. Keep the concurrency slot and resources until it actually finishes.
            await Task.Run(() => RenderCore(images.FoPath, temporary, configuration), CancellationToken.None).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            long size = new FileInfo(temporary).Length;
            if (size < 5 || size > options.MaxPdfBytes)
            {
                throw new IOException("Invalid PDF size.");
            }

            await using (FileStream stream = File.OpenRead(temporary))
            {
                byte[] header = new byte[5];
                await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
                if (!header.AsSpan().SequenceEqual("%PDF-"u8))
                {
                    throw new IOException("FOP did not produce a PDF.");
                }
            }
            stage = ConversionStage.Publish;
            AtomicOutput.Publish(temporary, pdfPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ConversionException(stage, $"FO → PDF failed during {stage}.", ex);
        }
        finally
        {
            if (temporary is not null && File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            slots.Release();
        }
    }

    private static void RenderCore(string fo, string pdf, string configuration)
    {
        java.lang.System.setProperty("java.awt.headless", "true");
        org.apache.fop.apps.FopFactory factory = new org.apache.fop.apps.FopConfParser(new java.io.File(configuration)).getFopFactoryBuilder().build();
        org.apache.fop.apps.FOUserAgent userAgent = factory.newFOUserAgent();
        var events = new FailureListener();
        userAgent.getEventBroadcaster().addEventListener(events);
        using var output = new java.io.FileOutputStream(pdf);
        org.apache.fop.apps.Fop fop = factory.newFop("application/pdf", userAgent, output);
        javax.xml.transform.Transformer transformer = javax.xml.transform.TransformerFactory.newInstance().newTransformer();
        transformer.transform(new javax.xml.transform.stream.StreamSource(new java.io.File(fo)),
            new javax.xml.transform.sax.SAXResult(fop.getDefaultHandler()));
        if (events.Failed || fop.getResults().getPageCount() == 0)
        {
            throw new IOException("FOP reported a rendering error or produced no pages.");
        }
    }

    private sealed class FailureListener : org.apache.fop.events.EventListener
    {
        public bool Failed
        {
            get; private set;
        }
        public void processEvent(org.apache.fop.events.Event value)
        {
            org.apache.fop.events.model.EventSeverity severity = value.getSeverity();
            if (severity == org.apache.fop.events.model.EventSeverity.ERROR || severity == org.apache.fop.events.model.EventSeverity.FATAL)
            {
                Failed = true;
            }
        }
    }

    public void Dispose() => slots.Dispose();
}
