namespace Company.XmlFo;

public sealed class XmlPdfGenerator(IXsltFoTransformer transformer, IFoPdfRenderer renderer) : IXmlPdfGenerator
{
    public async Task GenerateFileAsync(string xmlPath, string xsltPath, string pdfPath, CancellationToken cancellationToken = default)
    {
        // Validate target before any work; use its directory so publication stays on the same filesystem.
        string fo = AtomicOutput.Prepare(pdfPath, xmlPath, xsltPath) + ".fo";
        try
        {
            await transformer.TransformFileAsync(xmlPath, xsltPath, fo, cancellationToken).ConfigureAwait(false);
            await renderer.RenderFileAsync(fo, pdfPath, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(fo))
            {
                File.Delete(fo);
            }
        }
    }
}
