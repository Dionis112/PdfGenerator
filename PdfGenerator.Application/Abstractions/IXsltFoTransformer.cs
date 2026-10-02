namespace Company.XmlFo;

public interface IXsltFoTransformer
{
    Task TransformFileAsync(string xmlPath, string xsltPath, string foPath, CancellationToken cancellationToken = default);
}
