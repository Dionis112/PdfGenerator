namespace Company.XmlFo;

public interface IXmlPdfGenerator
{
    Task GenerateFileAsync(string xmlPath, string xsltPath, string pdfPath, CancellationToken cancellationToken = default);
}
