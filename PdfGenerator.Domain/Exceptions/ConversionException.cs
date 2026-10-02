namespace Company.XmlFo;

public sealed class ConversionException(ConversionStage stage, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public ConversionStage Stage { get; } = stage;
}
