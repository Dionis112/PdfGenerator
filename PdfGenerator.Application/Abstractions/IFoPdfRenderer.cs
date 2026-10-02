namespace Company.XmlFo;

public interface IFoPdfRenderer
{
    Task RenderFileAsync(string foPath, string pdfPath, CancellationToken cancellationToken = default);
}
